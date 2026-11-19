using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using Serilog;

namespace opentuner.MediaPlayers.VLC
{
    public class VLCMediaPlayer : OTMediaPlayer
    {
        LibVLC libVLC = new LibVLC("--aout=directsound");
        Media media;
        TSStreamMediaInput media_input;
        LibVLCSharp.WinForms.VideoView videoView;

        MediaPlayer _mediaplayer;
        private int videoUpdateGeneration;
        // Guards the swap of _mediaplayer/media/media_input: Stop/Play can be
        // called from the UI thread and from source status threads.
        private readonly object playerSwapLock = new object();

        int player_volume;

        public override event EventHandler<MediaStatus> onVideoOut;

        int _id = 0;

        CircularBuffer ts_data_queue;
        public VLCMediaPlayer(LibVLCSharp.WinForms.VideoView VideoView)
        {
            libVLC.Log += LibVLC_Log;
            videoView = VideoView;
        }

        private void LibVLC_Log(object sender, LogEventArgs e)
        {
        }

        public override string GetName()
        {
            return "VLC";
        }

        private void updateVideoPlayer(MediaPlayer newPlayer, bool play)
        {
            // Invalidate queued UI updates whenever a newer tune or Stop occurs.
            int generation = System.Threading.Interlocked.Increment(ref videoUpdateGeneration);
            if (videoView == null || videoView.IsDisposed) return;

            MethodInvoker attach = delegate
            {
                if (generation != System.Threading.Volatile.Read(ref videoUpdateGeneration) ||
                    videoView == null || videoView.IsDisposed) return;
                // A retune may have disposed the former MediaPlayer while the
                // UI callback waited in the message queue.
                if (newPlayer != null && !Object.ReferenceEquals(newPlayer, _mediaplayer))
                    return;
                if (!videoView.IsHandleCreated) return;

                try
                {
                    // VideoView itself owns and binds the native HWND.
                    // Never set MediaPlayer.Hwnd separately: doing so races
                    // handle recreation when the renderer is reparented.
                    videoView.MediaPlayer = newPlayer;
                    if (newPlayer != null)
                    {
                        videoView.Visible = true;
                        videoView.BringToFront();
                    }
                    if (play && newPlayer != null && media != null &&
                        Object.ReferenceEquals(newPlayer, _mediaplayer))
                        newPlayer.Play(media);
                }
                catch (ObjectDisposedException ex)
                {
                    Log.Warning(ex, "VLC: VideoView disposed during player switch");
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning(ex, "VLC: VideoView not available during player switch");
                }
            };

            if (videoView.InvokeRequired)
            {
                try { videoView.BeginInvoke(attach); }
                catch (InvalidOperationException) { }
            }
            else attach();
        }

        private void MediaPlayer_EncounteredError(object sender, EventArgs e)
        {
            Log.Information("VLC: Error: " + libVLC.LastLibVLCError);
        }

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            Log.Information("VLC: Playing ");
        }

        private void MediaPlayer_Stopped(object sender, EventArgs e)
        {
            Log.Information("VLC: Stopped");
        }
        
        public override void Initialize(CircularBuffer TSDataQueue)
        {
            ts_data_queue = TSDataQueue;
            CreatePlayerObjects();
        }

        private void CreatePlayerObjects()
        {
            _mediaplayer = new MediaPlayer(libVLC);
            _mediaplayer.Stopped += MediaPlayer_Stopped;
            _mediaplayer.Playing += MediaPlayer_Playing;
            _mediaplayer.EncounteredError += MediaPlayer_EncounteredError;
            _mediaplayer.Vout += MediaPlayer_Vout;

            _mediaplayer.EnableMouseInput = false;
            _mediaplayer.EnableKeyInput = false;

            _mediaplayer.SetMarqueeInt(VideoMarqueeOption.Size, 20);
            _mediaplayer.SetMarqueeInt(VideoMarqueeOption.X, 10);
            _mediaplayer.SetMarqueeInt(VideoMarqueeOption.Y, 10);

            media_input = new TSStreamMediaInput(ts_data_queue);
            media = new Media(libVLC, media_input);

            MediaConfiguration media_config = new MediaConfiguration();
            media_config.EnableHardwareDecoding = false;
            media.AddOption(media_config);
        }

        private void MediaPlayer_Vout(object sender, MediaPlayerVoutEventArgs e)
        {
            if (videoView.MediaPlayer == null)
                return;

            videoView.MediaPlayer.Volume = player_volume;

            MediaStatus media_status = new MediaStatus();

            // A retune may have released this media on another thread.
            Media currentMedia = media;
            if (currentMedia == null) return;

            foreach (var track in currentMedia.Tracks)
            {
                switch (track.TrackType)
                {
                    case TrackType.Audio:
                        media_status.AudioChannels = track.Data.Audio.Channels;
                        media_status.AudioCodec = currentMedia.CodecDescription(TrackType.Audio, track.Codec);
                        media_status.AudioRate = track.Data.Audio.Rate;
                        break;
                    case TrackType.Video:
                        media_status.VideoCodec = currentMedia.CodecDescription(TrackType.Video, track.Codec);
                        media_status.VideoWidth = track.Data.Video.Width;
                        media_status.VideoHeight = track.Data.Video.Height;
                        Log.Information("VLC video track: " + media_status.VideoCodec + " " + media_status.VideoWidth.ToString() + "x" + media_status.VideoHeight.ToString());
                        break;
                }
            }

            if (onVideoOut != null)
                onVideoOut(this, media_status);
        }

        public override void SnapShot(string FileName)
        {
            if (videoView.MediaPlayer != null)
                videoView.MediaPlayer.TakeSnapshot(0, FileName, 0, 0);
        }

        public override void Close()
        {
            Stop();
            try { libVLC?.Dispose(); } catch { }
        }

        public override void Stop()
        {
            Log.Information("VLC: Stop Command");

            MediaPlayer oldPlayer;
            Media oldMedia;
            TSStreamMediaInput oldInput;
            lock (playerSwapLock)
            {
                oldPlayer = _mediaplayer;
                oldMedia = media;
                oldInput = media_input;
                _mediaplayer = null;
                media = null;
                media_input = null;
            }

            // Let VLC's input thread finish its current Read immediately.
            try { if (oldInput != null) oldInput.end = true; } catch { }

            if (oldPlayer != null)
            {
                try
                {
                    oldPlayer.Stopped -= MediaPlayer_Stopped;
                    oldPlayer.Playing -= MediaPlayer_Playing;
                    oldPlayer.EncounteredError -= MediaPlayer_EncounteredError;
                    oldPlayer.Vout -= MediaPlayer_Vout;
                }
                catch { }
            }

            // Never stop/dispose a LibVLC MediaPlayer on the UI thread: releasing
            // it waits for VLC's video-output thread, which in turn needs the UI
            // thread to service the video window -> deadlock ("Not Responding"),
            // seen when two receivers retuned at the same moment. Detach it from
            // the VideoView on the UI thread, then release it on a pool thread.
            ReleasePlayerLater(oldPlayer, oldMedia, oldInput);
        }

        private void ReleasePlayerLater(MediaPlayer oldPlayer, Media oldMedia, TSStreamMediaInput oldInput)
        {
            // Invalidate any queued attach for the player being released.
            System.Threading.Interlocked.Increment(ref videoUpdateGeneration);
            if (oldPlayer == null && oldMedia == null && oldInput == null) return;

            WaitCallback release = delegate
            {
                try { if (oldPlayer != null) oldPlayer.Stop(); } catch (Exception ex) { Log.Warning(ex, "VLC: stop on release failed"); }
                try { if (oldPlayer != null) oldPlayer.Dispose(); } catch (Exception ex) { Log.Warning(ex, "VLC: player dispose failed"); }
                try { if (oldMedia != null) oldMedia.Dispose(); } catch { }
                try { if (oldInput != null) oldInput.Dispose(); } catch { }
            };

            // Detach first, so the VideoView never touches a released player.
            MethodInvoker detach = delegate
            {
                try
                {
                    if (videoView != null && !videoView.IsDisposed &&
                        oldPlayer != null && Object.ReferenceEquals(videoView.MediaPlayer, oldPlayer))
                        videoView.MediaPlayer = null;
                }
                catch (Exception ex) { Log.Warning(ex, "VLC: VideoView detach failed"); }
                ThreadPool.QueueUserWorkItem(release);
            };

            try
            {
                if (videoView == null || videoView.IsDisposed || !videoView.IsHandleCreated)
                    ThreadPool.QueueUserWorkItem(release);
                else if (videoView.InvokeRequired)
                    videoView.BeginInvoke(detach);
                else
                    detach();
            }
            catch (InvalidOperationException)
            {
                // The handle went away between the checks; nothing is attached now.
                ThreadPool.QueueUserWorkItem(release);
            }
        }

        public override void Play()
        {
            Log.Information("VLC: Play Command");

            Stop();
            ts_data_queue.Clear();
            MediaPlayer newPlayer;
            lock (playerSwapLock)
            {
                CreatePlayerObjects();
                media_input.ts_sync = false;
                media_input.end = false;
                newPlayer = _mediaplayer;
            }
            updateVideoPlayer(newPlayer, true);
        }

        public override void SetVolume(int Volume)
        {
            player_volume = Volume;

            if (videoView.MediaPlayer != null)
                videoView.MediaPlayer.Volume = player_volume;
        }

        public override int GetVolume()
        {
            return player_volume;
        }

        public override int getID()
        {
            return _id;
        }

        public override void Initialize(CircularBuffer TSDataQueue, int ID)
        {
            _id = ID;
            Initialize(TSDataQueue);
        }
    }
}

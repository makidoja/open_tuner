using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        private delegate void updateMediaPlayerDelegate(MediaPlayer newPlayer, bool play);

        private void updateVideoPlayer(MediaPlayer newPlayer, bool play)
        {
            if (videoView == null || videoView.IsDisposed)
                return;

            if (videoView.InvokeRequired)
            {
                try
                {
                    videoView.BeginInvoke(new updateMediaPlayerDelegate(updateVideoPlayer), new object[] { newPlayer, play });
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            // LibVLC can decode audio without a usable video target. Make sure the WinForms
            // VideoView has a live native handle before attaching the MediaPlayer, then bind
            // the player explicitly to that HWND. This avoids the audio-only condition seen
            // when the view is created inside a SplitterPanel before playback begins.
            if (!videoView.IsHandleCreated)
            {
                IntPtr createHandle = videoView.Handle;
            }

            videoView.Visible = true;
            videoView.MediaPlayer = newPlayer;

            if (newPlayer != null)
            {
                IntPtr targetHwnd = videoView.Handle;
                if (targetHwnd != IntPtr.Zero)
                {
                    newPlayer.Hwnd = targetHwnd;
                    Log.Information("VLC video target HWND: " + targetHwnd.ToInt64().ToString());
                }

                // Keep the video surface above any themed background panels. The info/volume
                // overlays are re-added by OpenTuner as required and do not own the VLC HWND.
                videoView.BringToFront();
            }

            if (play && newPlayer != null && media != null)
            {
                Thread.Sleep(10);
                Log.Information("VLC: starting playback on HWND " + newPlayer.Hwnd.ToString());
                newPlayer.Play(media);
            }
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

            foreach (var track in media.Tracks)
            {
                switch (track.TrackType)
                {
                    case TrackType.Audio:
                        media_status.AudioChannels = track.Data.Audio.Channels;
                        media_status.AudioCodec = media.CodecDescription(TrackType.Audio, track.Codec);
                        media_status.AudioRate = track.Data.Audio.Rate;
                        break;
                    case TrackType.Video:
                        media_status.VideoCodec = media.CodecDescription(TrackType.Video, track.Codec);
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

            try
            {
                if (media_input != null)
                    media_input.end = true;
            }
            catch { }

            try { updateVideoPlayer(null, false); } catch { }

            if (_mediaplayer != null)
            {
                try
                {
                    _mediaplayer.Stopped -= MediaPlayer_Stopped;
                    _mediaplayer.Playing -= MediaPlayer_Playing;
                    _mediaplayer.EncounteredError -= MediaPlayer_EncounteredError;
                    _mediaplayer.Vout -= MediaPlayer_Vout;
                }
                catch { }
                try { _mediaplayer.Dispose(); } catch { }
                _mediaplayer = null;
            }

            if (media != null)
            {
                try { media.Dispose(); } catch { }
                media = null;
            }

            if (media_input != null)
            {
                try { media_input.Dispose(); } catch { }
                media_input = null;
            }
        }

        public override void Play()
        {
            Log.Information("VLC: Play Command");

            Stop();
            ts_data_queue.Clear();
            CreatePlayerObjects();
            media_input.ts_sync = false;
            media_input.end = false;
            updateVideoPlayer(_mediaplayer, true);
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

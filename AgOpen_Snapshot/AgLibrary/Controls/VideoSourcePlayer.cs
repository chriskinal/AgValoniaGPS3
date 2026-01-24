using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Accord.Video;

namespace AgLibrary.Controls
{
    public partial class VideoSourcePlayer : Control
    {
        // video source to play
        private IVideoSource videoSource = null;
        // last received frame from the video source
        private Bitmap currentFrame = null;
        // converted version of the current frame (in the case if current frame is a 16 bpp 
        // per color plane image, then the converted image is its 8 bpp version for rendering)
        private Bitmap convertedFrame = null;
        // last error message provided by video source
        private string lastMessage = null;
        // controls border color
        private Color borderColor = Color.Black;

        private bool keepRatio = false;
        private bool needSizeUpdate = false;
        private bool firstFrameNotProcessed = true;
        private volatile bool requestedToStop = false;

        // dummy object to lock for synchronization
        private readonly object sync = new object();

        /// <summary>
        /// Gets or sets whether the player should keep the aspect ratio of the images being shown.
        /// </summary>
        /// 
        [DefaultValue(false)]
        public bool KeepAspectRatio
        {
            get { return keepRatio; }
            set
            {
                keepRatio = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Control's border color.
        /// </summary>
        /// 
        /// <remarks><para>Specifies color of the border drawn around video frame.</para></remarks>
        /// 
        [DefaultValue(typeof(Color), "Black")]
        public Color BorderColor
        {
            get { return borderColor; }
            set
            {
                borderColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Video source to play.
        /// </summary>
        /// 
        /// <remarks><para>The property sets the video source to play. After setting the property the
        /// <see cref="Start"/> method should be used to start playing the video source.</para>
        /// 
        /// <para><note>Trying to change video source while currently set video source is still playing
        /// will generate an exception. Use <see cref="IsRunning"/> property to check if current video
        /// source is still playing or <see cref="Stop"/> or <see cref="SignalToStop"/> and <see cref="WaitForStop"/>
        /// methods to stop current video source.</note></para>
        /// </remarks>
        /// 
        /// <exception cref="Exception">Video source can not be changed while current video source is still running.</exception>
        /// 
        [Browsable(false)]
        public IVideoSource VideoSource
        {
            get { return videoSource; }
            set
            {
                CheckForCrossThreadAccess();

                // detach events
                if (videoSource != null)
                {
                    videoSource.NewFrame -= new NewFrameEventHandler(videoSource_NewFrame);
                    videoSource.VideoSourceError -= new VideoSourceErrorEventHandler(videoSource_VideoSourceError);
                    videoSource.PlayingFinished -= new PlayingFinishedEventHandler(videoSource_PlayingFinished);
                }

                lock (sync)
                {
                    if (currentFrame != null)
                    {
                        currentFrame.Dispose();
                        currentFrame = null;
                    }
                }

                videoSource = value;

                // atach events
                if (videoSource != null)
                {
                    videoSource.NewFrame += new NewFrameEventHandler(videoSource_NewFrame);
                    videoSource.VideoSourceError += new VideoSourceErrorEventHandler(videoSource_VideoSourceError);
                    videoSource.PlayingFinished += new PlayingFinishedEventHandler(videoSource_PlayingFinished);
                }

                lastMessage = null;
                needSizeUpdate = true;
                firstFrameNotProcessed = true;
                // update the control
                Invalidate();
            }
        }

        /// <summary>
        /// State of the current video source.
        /// </summary>
        /// 
        /// <remarks><para>Current state of the current video source object - running or not.</para></remarks>
        /// 
        [Browsable(false)]
        public bool IsRunning
        {
            get
            {
                CheckForCrossThreadAccess();

                return videoSource != null && videoSource.IsRunning;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSourcePlayer"/> class.
        /// </summary>
        public VideoSourcePlayer()
        {
            InitializeComponent();

            // update control style
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                ControlStyles.DoubleBuffer | ControlStyles.UserPaint, true);
        }

        // Check if the control is accessed from a none UI thread
        private void CheckForCrossThreadAccess()
        {
            // force handle creation, so InvokeRequired() will use it instead of searching through parent's chain
            if (!IsHandleCreated)
            {
                CreateControl();

                // if the control is not Visible, then CreateControl() will not be enough
                if (!IsHandleCreated)
                {
                    CreateHandle();
                }
            }

            if (InvokeRequired)
            {
                throw new InvalidOperationException("Cross thread access to the control is not allowed.");
            }
        }

        /// <summary>
        /// Start video source and displaying its frames.
        /// </summary>
        public void Start()
        {
            CheckForCrossThreadAccess();

            requestedToStop = false;

            if (videoSource != null)
            {
                firstFrameNotProcessed = true;

                videoSource.Start();
                Invalidate();
            }
        }

        /// <summary>
        /// Stop video source.
        /// </summary>
        /// 
        /// <remarks><para>The method stops video source by calling its <see cref="Accord.Video.IVideoSource.Stop"/>
        /// method, which abourts internal video source's thread. Use <see cref="SignalToStop"/> and
        /// <see cref="WaitForStop"/> for more polite video source stopping, which gives a chance for
        /// video source to perform proper shut down and clean up.
        /// </para></remarks>
        /// 
        public void Stop()
        {
            CheckForCrossThreadAccess();

            requestedToStop = true;

            if (videoSource != null)
            {
                videoSource.Stop();

                if (currentFrame != null)
                {
                    currentFrame.Dispose();
                    currentFrame = null;
                }

                Invalidate();
            }
        }

        /// <summary>
        /// Signal video source to stop. 
        /// </summary>
        /// 
        /// <remarks><para>Use <see cref="WaitForStop"/> method to wait until video source
        /// stops.</para></remarks>
        /// 
        public void SignalToStop()
        {
            CheckForCrossThreadAccess();

            requestedToStop = true;

            videoSource?.SignalToStop();
        }

        /// <summary>
        /// Wait for video source has stopped. 
        /// </summary>
        /// 
        /// <remarks><para>Waits for video source stopping after it was signaled to stop using
        /// <see cref="SignalToStop"/> method. If <see cref="SignalToStop"/> was not called, then
        /// it will be called automatically.</para></remarks>
        /// 
        public void WaitForStop()
        {
            CheckForCrossThreadAccess();

            if (!requestedToStop)
            {
                SignalToStop();
            }

            if (videoSource != null)
            {
                videoSource.WaitForStop();

                if (currentFrame != null)
                {
                    currentFrame.Dispose();
                    currentFrame = null;
                }

                Invalidate();
            }
        }

        // Paint control
        private void VideoSourcePlayer_Paint(object sender, PaintEventArgs e)
        {
            if (!Visible)
            {
                return;
            }

            // is it required to update control's size/position
            if (needSizeUpdate || firstFrameNotProcessed)
            {
                needSizeUpdate = false;
            }

            lock (sync)
            {
                Graphics g = e.Graphics;
                Rectangle rect = ClientRectangle;
                Pen borderPen = new Pen(borderColor, 1);

                // draw rectangle
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

                if (videoSource != null)
                {
                    if ((currentFrame != null) && (lastMessage == null))
                    {
                        Bitmap frame = convertedFrame ?? currentFrame;

                        if (keepRatio)
                        {
                            double ratio = (double)frame.Width / frame.Height;
                            Rectangle newRect = rect;

                            if (rect.Width < rect.Height * ratio)
                            {
                                newRect.Height = (int)(rect.Width / ratio);
                            }
                            else
                            {
                                newRect.Width = (int)(rect.Height * ratio);
                            }

                            newRect.X = (rect.Width - newRect.Width) / 2;
                            newRect.Y = (rect.Height - newRect.Height) / 2;

                            g.DrawImage(frame, newRect.X + 1, newRect.Y + 1, newRect.Width - 2, newRect.Height - 2);
                        }
                        else
                        {
                            // draw current frame
                            g.DrawImage(frame, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                        }

                        firstFrameNotProcessed = false;
                    }
                    else
                    {
                        // create font and brush
                        SolidBrush drawBrush = new SolidBrush(ForeColor);

                        g.DrawString(lastMessage ?? "Connecting ...", Font, drawBrush, new PointF(5, 5));

                        drawBrush.Dispose();
                    }
                }

                borderPen.Dispose();
            }
        }

        // On new frame ready
        private void videoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!requestedToStop)
            {
                Bitmap newFrame = (Bitmap)eventArgs.Frame.Clone();

                // now update current frame of the control
                lock (sync)
                {
                    // dispose previous frame
                    if (currentFrame != null)
                    {
                        if (currentFrame.Size != eventArgs.Frame.Size)
                        {
                            needSizeUpdate = true;
                        }

                        currentFrame.Dispose();
                        currentFrame = null;
                    }
                    if (convertedFrame != null)
                    {
                        convertedFrame.Dispose();
                        convertedFrame = null;
                    }

                    currentFrame = newFrame;
                    lastMessage = null;

                    // check if conversion is required to lower bpp rate
                    if ((currentFrame.PixelFormat == PixelFormat.Format16bppGrayScale) ||
                         (currentFrame.PixelFormat == PixelFormat.Format48bppRgb) ||
                         (currentFrame.PixelFormat == PixelFormat.Format64bppArgb))
                    {
                        convertedFrame = Accord.Imaging.Image.Convert16bppTo8bpp(currentFrame);
                    }
                }

                // update control
                Invalidate();
            }
        }

        // Error occured in video source
        private void videoSource_VideoSourceError(object sender, VideoSourceErrorEventArgs eventArgs)
        {
            lastMessage = eventArgs.Description;
            Invalidate();
        }

        // Video source has finished playing video
        private void videoSource_PlayingFinished(object sender, ReasonToFinishPlaying reason)
        {
            switch (reason)
            {
                case ReasonToFinishPlaying.EndOfStreamReached:
                    lastMessage = "Video has finished";
                    break;

                case ReasonToFinishPlaying.StoppedByUser:
                    lastMessage = "Video was stopped";
                    break;

                case ReasonToFinishPlaying.DeviceLost:
                    lastMessage = "Video device was unplugged";
                    break;

                case ReasonToFinishPlaying.VideoSourceError:
                    lastMessage = "Video has finished because of error in video source";
                    break;

                default:
                    lastMessage = "Video has finished for unknown reason";
                    break;
            }
            Invalidate();
        }
    }
}

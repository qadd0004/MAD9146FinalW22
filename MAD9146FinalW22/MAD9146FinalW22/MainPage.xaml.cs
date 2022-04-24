// Mohsen Qaddoura - Karim Shaloh, April 18, 2022
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace MAD9146FinalW22
{
    public partial class MainPage : ContentPage
    {

        #region Fields

        private const string BASEURL = "https://source.unsplash.com/random/";
        private const string PORTRAITRESOLUTION = "960x1280";
        private const string LANDSCAPERESOLUTION = "1280x960";

        private readonly float density = 1.0f; // Density defaults to 1.0

        private float scale = 1.0f;

        private int sourceImageWidth = 0;  // actual image width
        private int sourceImageHeight = 0; // actual image height

        private float displayedImageWidth = 0.0f;  // displayed image width
        private float displayedImageHeight = 0.0f; // displayed image height

        private int leftPaddingAdjustment = 0;
        private int topPaddingAdjustment = 0;

        #endregion

        #region Face API

        private const string FACEAPIKEY = "0fcb3896f30847d7b8c894b969de35c9";
        private const string FACEENDPOINT = "https://face-recogn.cognitiveservices.azure.com/";

        #endregion

        #region API fields

        private const string APIKEY = "6529475af4cf4da2a05d17bd3796980f";
        private const string ENDPOINT = "https://mohsen-vision.cognitiveservices.azure.com/";

        #endregion

        #region Face Fields

        private IList<FaceAttributeType> faceAttributes =
            new FaceAttributeType[] {
                FaceAttributeType.Age,
                FaceAttributeType.Gender,
                FaceAttributeType.Emotion,
                FaceAttributeType.Smile,
                FaceAttributeType.Hair,
                FaceAttributeType.Glasses,
                FaceAttributeType.Accessories
            };

        private Microsoft.Azure.CognitiveServices.Vision.Face.Models.FaceRectangle[] faceRectangles = null;
        private string[] faceDescriptions;

        private MemoryStream faceImageMemoryStream = new MemoryStream();

        #endregion

       
        const string DELIMETER = ", ";

        private CancellationTokenSource cts = null;

        #region Vision Fields

        private readonly List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>
        {
          VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
          VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
          VisualFeatureTypes.Tags
        };

        #endregion

        #region Constructor
        public MainPage()
        {
            InitializeComponent();

            if (DeviceInfo.Platform == DevicePlatform.UWP)
            {
                TheActivityIndicator.Scale = 3.0;
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                TheActivityIndicator.Scale = 2.0;
            }

            // get and save device density:
            density = (float)DeviceDisplay.MainDisplayInfo.Density;
        }

        #endregion

        
        #region Button Clicked methods
        private async void ImageButton_Clicked(object sender, EventArgs e)
        {
            var file = await MediaPicker.PickPhotoAsync(
                new MediaPickerOptions
                {
                    Title = "Please choose an image."
                });

            TheActivityIndicator.IsRunning = true;

            if (file != null) //null if user cancels
            {
                try
                {
                    var stream = await file.OpenReadAsync();

                    var result = await GetImageDescription(stream);

                    ProcessImageResults(result);

                    TheImage.Source = ImageSource.FromStream(() => stream);
                }
                catch (ComputerVisionErrorResponseException ex)
                {
                    TheResults.Text = ex.Body.Error.Code + Environment.NewLine + ex.Body.Error.Message;
                }
                catch (Exception ex)
                {
                    TheResults.Text = ex.Message;
                }
            }

            TheActivityIndicator.IsRunning = false;
        }

        private void FaceButton_Clicked(object sender, EventArgs e)
        {
            GetFaceData();
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingsPage());
        }

        private async void CameraButton_Clicked(object sender, EventArgs e)
        {
            if (!MediaPicker.IsCaptureSupported)
            {
                await DisplayAlert("No camera", ":(No camera available)", "OK");
                return;
            }

            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Camera Permission", ":(App camera permission was denied)", "OK");

                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            var file = await MediaPicker.CapturePhotoAsync(
                new MediaPickerOptions
                {
                    Title = "Please take picture."
                });

            if (file == null)
            {
                return;
            }

        }

        private async void WebImageButton_Clicked(object sender, EventArgs e)
        {
            TheActivityIndicator.IsRunning = true;

            Uri webImageUri = new Uri($"{BASEURL}{PORTRAITRESOLUTION}");

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (var response = await client.GetStreamAsync(webImageUri))
                    {
                        var memoryStream = new MemoryStream();

                        await response.CopyToAsync(memoryStream);

                        memoryStream.Position = 0;

                        try
                        {
                            var result = await GetImageDescription(memoryStream);

                            ProcessImageResults(result);

                            memoryStream.Position = 0;

                            TheImage.Source = ImageSource.FromStream(() => memoryStream);
                        }

                        catch (ComputerVisionErrorResponseException ex)
                        {
                            TheResults.Text = ex.Body.Error.Code + Environment.NewLine + ex.Body.Error.Message;
                        }

                        catch (Exception ex)
                        {
                            TheResults.Text = ex.Message;
                        }
                    }
                }

                TheActivityIndicator.IsRunning = false;

            }
            catch (Exception ex)
            {
                TheResults.Text = "Failed to load web image" + ex.Message;
            }

            TheActivityIndicator.IsRunning = false;

        }

        #endregion

        #region Image Analysis Methods

        private void TheImage_SizeChanged(object sender, EventArgs e)
        {
            displayedImageWidth = (float)TheImage.Width;
            displayedImageHeight = (float)TheImage.Height;

            scale = displayedImageWidth / sourceImageWidth;
            scale *= density;
        }

        private async Task<ImageAnalysis> GetImageDescription(Stream imageStream)
        {
            ComputerVisionClient visionClient = new ComputerVisionClient(
                new Microsoft.Azure.CognitiveServices.Vision.ComputerVision.ApiKeyServiceClientCredentials(APIKEY),
                new System.Net.Http.DelegatingHandler[] { })
            {
                Endpoint = ENDPOINT
            };

            MemoryStream memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            imageStream.Position = 0;
            memoryStream.Position = 0;

            ResetFaceUIAndData();

            var results = await visionClient.AnalyzeImageInStreamAsync(memoryStream, features, null);

            if (results.Faces.Count > 0 && App.GetFaceData)
            {
                FaceButton.IsEnabled = true;

                await imageStream.CopyToAsync(faceImageMemoryStream);
                faceImageMemoryStream.Position = 0;
                imageStream.Position = 0;

            }

            sourceImageWidth = results.Metadata.Width;
            sourceImageHeight = results.Metadata.Height;

            return results;
        }

        private void ProcessImageResults(ImageAnalysis result)
        {
            if (result.Description.Captions.Count != 0) //did get some data
            {
                if (result.Description.Captions[0].ToString().Length > 0)
                {
                    
                    StringBuilder stringBuilder = new StringBuilder();

                    foreach (var item in result.Description.Captions)
                    {

                        stringBuilder.Append(item.Text);
                    }

                    stringBuilder.Append(Environment.NewLine);
                    stringBuilder.Append($"Confidence: {result.Description.Captions[0].Confidence * 100:F1}%");
                    stringBuilder.Append(Environment.NewLine);

                    foreach (var tag in result.Tags)
                    {
                        if (tag != result.Tags.Last())
                        {
                            stringBuilder.Append(tag.Name + DELIMETER);
                        }
                        else
                        {
                            stringBuilder.Append(tag.Name);
                        }
                    }

                    TheResults.Text = stringBuilder.ToString();
                }
            }
            else
            {
                TheResults.Text = "Nothing was Recognized";
            }
        }

        #endregion

        #region Speech methods
        private async void Speak()
        {
            cts = new CancellationTokenSource();
            await TextToSpeech.SpeakAsync(TheResults.Text, cts.Token);
        }

        private void CancelSpeak()
        {
            if (cts == null) { return; }

            cts.Cancel();
            cts = null;
        }

        private void TheResults_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!App.UseSpeech || e.PropertyName != "Text")
            {
                return;
            }

            if (!string.IsNullOrEmpty(TheResults.Text))
            {
                CancelSpeak();
                Speak();
            }
        }

        #endregion

        #region Face methods
        private void ResetFaceUIAndData()
        {
            faceRectangles = null;
            TheCanvas.InvalidateSurface();
            FaceButton.IsEnabled = false;
        }

        private async void GetFaceData()
        {
            FaceClient faceClient = new FaceClient(
                new Microsoft.Azure.CognitiveServices.Vision.Face.ApiKeyServiceClientCredentials(FACEAPIKEY),
                new System.Net.Http.DelegatingHandler[] { })
            {
                Endpoint = FACEENDPOINT
            };

            MemoryStream memoryStream = new MemoryStream();
            await faceImageMemoryStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            faceImageMemoryStream.Position = 0;

            TheActivityIndicator.IsRunning = true;

            try
            {
                IList<DetectedFace> faceList = await
                    faceClient.Face.DetectWithStreamAsync(memoryStream, true, false, faceAttributes);

                if (faceList != null)
                {
                    var faceRects = faceList.Select(face => face.FaceRectangle);
                    faceRectangles = faceRects.ToArray();

                    faceDescriptions = new String[faceList.Count];

                    for (int i = 0; i < faceList.Count; i++)
                    {
                        faceDescriptions[i] = GenerateFaceDescription(faceList[i]);
                    }

                    TheCanvas.InvalidateSurface();

                }

            }

            catch (APIErrorException ex)
            {
                TheResults.Text = $"{ex.Message}{Environment.NewLine}" +
                     $"{ex.Response.Content}";
            }
            catch (Exception ex)
            {
                TheResults.Text = ex.Message;
            }

            TheActivityIndicator.IsRunning = false;

        }

        private string GenerateFaceDescription(DetectedFace detectedFace)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("Desciption: ");
            stringBuilder.Append(Environment.NewLine);

            stringBuilder.Append($" - " + detectedFace.FaceAttributes.Gender);
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append($" - " + detectedFace.FaceAttributes.Age);
            stringBuilder.Append(Environment.NewLine);

            float glasses = (float)detectedFace.FaceAttributes.Glasses;
            if (glasses > 0.0)
            {
                stringBuilder.Append($" - has glasses");
                stringBuilder.Append(Environment.NewLine);
            }
            else
            {
                stringBuilder.Append($" - no glasses");
                stringBuilder.Append(Environment.NewLine);
            }

            float smile = (float)detectedFace.FaceAttributes.Smile;
            if (smile >= 0.1)
            {
                stringBuilder.Append($" - smile {smile * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append("Emotions: ");
            stringBuilder.Append(Environment.NewLine);

            float sadness = (float)detectedFace.FaceAttributes.Emotion.Sadness;
            if (sadness >= 0.1)
            {
                stringBuilder.Append($" - sadness {sadness * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float surprise = (float)detectedFace.FaceAttributes.Emotion.Surprise;
            if (surprise >= 0.1)
            {
                stringBuilder.Append($" - surprise {surprise * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float happiness = (float)detectedFace.FaceAttributes.Emotion.Happiness;
            if (happiness >= 0.1)
            {
                stringBuilder.Append($" - happiness {happiness * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float neutral = (float)detectedFace.FaceAttributes.Emotion.Neutral;
            if (neutral >= 0.1)
            {
                stringBuilder.Append($" - neutral {neutral * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float anger = (float)detectedFace.FaceAttributes.Emotion.Anger;
            if (anger >= 0.1)
            {
                stringBuilder.Append($" - anger {anger * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float contempt = (float)detectedFace.FaceAttributes.Emotion.Contempt;
            if (contempt >= 0.1)
            {
                stringBuilder.Append($" - contempt {contempt * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float disgust = (float)detectedFace.FaceAttributes.Emotion.Disgust;
            if (disgust >= 0.1)
            {
                stringBuilder.Append($" - disgust {disgust * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            float fear = (float)detectedFace.FaceAttributes.Emotion.Fear;
            if (fear >= 0.1)
            {
                stringBuilder.Append($" - fear {fear * 100:F1}%");
                stringBuilder.Append(Environment.NewLine);
            }

            foreach (var hairColor in detectedFace.FaceAttributes.Hair.HairColor)
            {
                if (hairColor.Confidence >= 0.8)
                {
                    stringBuilder.Append($" - hair: {hairColor.Color.ToString()}");
                    stringBuilder.Append(Environment.NewLine);
                }
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Canvas methods
        private void TheCanvas_Touch(object sender, SkiaSharp.Views.Forms.SKTouchEventArgs e)
        {
            if (faceRectangles == null || e.MouseButton != SkiaSharp.Views.Forms.SKMouseButton.Left || e.ActionType != SkiaSharp.Views.Forms.SKTouchAction.Pressed)
            {
                return;
            }

            // Find the tapped point position relative to the image.
            SKPoint tappedPointXY = e.Location;

            for (int i = 0; i < faceRectangles.Length; ++i)
            {
                var faceRect = faceRectangles[i];
                double left = faceRect.Left * scale + leftPaddingAdjustment;
                double top = faceRect.Top * scale + topPaddingAdjustment;
                double width = faceRect.Width * scale;
                double height = faceRect.Height * scale;

                // Display the face description for this face if the mouse or touch was on this face rectangle.
                if (tappedPointXY.X >= left && tappedPointXY.X <= left + width && tappedPointXY.Y >= top && tappedPointXY.Y <= top + height)
                {
                    e.Handled = true;

                    DisplayAlert("Face Data", faceDescriptions[i], "OK");
                }
            }
        }

        private void TheCanvas_PaintSurface(object sender, SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs e)
        {
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear();

            if (faceRectangles == null)
            {
                return;
            }

            float strokeWidth = 8.0f; // set for hi-res display

            SKPaint redStrokePaint = new SKPaint()
            {
                StrokeWidth = strokeWidth,
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Red,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            if (faceRectangles.Length > 0) // we have at least one rectangle
            {
                leftPaddingAdjustment = (int)(((TheCanvas.Width - displayedImageWidth) / 2 * density) + 0.5f); // roundup
                topPaddingAdjustment = (int)(((TheCanvas.Height - displayedImageHeight) / 2 * density) + 0.5f); // roundup

                foreach (var faceRect in faceRectangles)
                {
                    canvas.DrawRect((faceRect.Left * scale + leftPaddingAdjustment),
                    (faceRect.Top * scale + topPaddingAdjustment),
                    (faceRect.Width * scale),
                    (faceRect.Height * scale),
                    redStrokePaint); // Stroke
                }
            }
        }

        #endregion
    }
}

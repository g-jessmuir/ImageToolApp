using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ImageToolApp
{
    public enum ToolState { Pan, Pencil }
    public sealed partial class MainPage : Page
    {
        List<CanvasBitmap> _layers;
        ToolState _toolState = ToolState.Pan;
        byte[] _color = { 0, 0, 255, 255 };
        byte[] BLANK = { 0, 0, 0, 0 };

        //transforms
        double _scale = 1;
        Vector2 _offset = new Vector2(0, 0);

        //dims
        Vector2 _canvasDims = new Vector2(0, 0);
        Vector2 _imgDims = new Vector2(0, 0);

        //state saves
        Vector2 _lastPointerPos = new Vector2(0, 0);
        Vector2 _pointerPos = new Vector2(0, 0);
        Vector2 _picPos = new Vector2(0, 0);
        bool _pointerPressed = false;

        public MainPage()
        {
            InitializeComponent();
        }

        private void RenderMain()
        {
            canvasControl.Invalidate();
        }

        private async void OpenFile_Button(object sender, RoutedEventArgs e)
        {
            FileOpenPicker fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.FileTypeFilter.Add(".jpg");
            fileOpenPicker.FileTypeFilter.Add(".png");
            fileOpenPicker.FileTypeFilter.Add(".bmp");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

            var inputFile = await fileOpenPicker.PickSingleFileAsync();

            if (inputFile == null)
            {
                // The user cancelled the picking operation
                return;
            }

            SoftwareBitmap inputBitmap;
            using (IRandomAccessStream stream = await inputFile.OpenAsync(FileAccessMode.Read))
            {
                // Create the decoder from the stream
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                // Get the SoftwareBitmap representation of the file
                inputBitmap = await decoder.GetSoftwareBitmapAsync();
            }

            if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8
                        || inputBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                inputBitmap = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            _layers = new List<CanvasBitmap>();
            _layers.Add(CanvasBitmap.CreateFromSoftwareBitmap(canvasControl.Device, inputBitmap));
            byte[] emptyBitmap = new byte[inputBitmap.PixelWidth * inputBitmap.PixelHeight * 4];
            _layers.Add(CanvasBitmap.CreateFromBytes(canvasControl.Device, emptyBitmap, inputBitmap.PixelWidth, 
                inputBitmap.PixelHeight, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized));
            _imgDims = new Vector2(_layers[0].SizeInPixels.Width, _layers[0].SizeInPixels.Height);

            RenderMain();
            inputBitmap.Dispose();
        }

        private void SaveAs_Button(object sender, RoutedEventArgs e)
        {

        }

        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_layers != null && _layers.Count > 0)
            {
                foreach (var layer in _layers)
                {
                    Rect destination = new Rect(_offset.x, _offset.y, _imgDims.x * _scale, _imgDims.y * _scale);
                    Rect source = new Rect(0, 0, _imgDims.x, _imgDims.y);
                    args.DrawingSession.DrawImage(layer, destination, source, 1.0f, CanvasImageInterpolation.NearestNeighbor);
                }
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            canvasControl.RemoveFromVisualTree();
            canvasControl = null;
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(canvasControl);
            _pointerPos.x = (int)pt.Position.X;
            _pointerPos.y = (int)pt.Position.Y;
            PointerPosText.Text = "Pointer Position: " + _pointerPos.x + "," + _pointerPos.y;
            PointerPosText.FontSize = 14;

            if (_layers == null || _layers.Count < 1)
                return;

            Vector2 pos;
            pos.x = (_pointerPos.x - _offset.x) / _scale;
            pos.y = (_pointerPos.y - _offset.y) / _scale;

            PointerPosPicText.Text = "Pic position: " + (int)pos.x + "," + (int)pos.y;
            PointerPosPicText.FontSize = 14;

            switch (_toolState)
            {
                case ToolState.Pan:
                    if (_pointerPressed)
                    {
                        _offset.x += _pointerPos.x - _lastPointerPos.x;
                        _offset.y += _pointerPos.y - _lastPointerPos.y;
                    }

                    OffsetText.Text = "Offset: " + (int)_offset.x + "," + (int)_offset.y;
                    ScaleText.Text = "Scale: " + _scale;
                    break;
                case ToolState.Pencil:
                    if (!_pointerPressed)
                        return;
                    if (pos.x >= 0 && pos.x < _imgDims.x && pos.y >= 0 && pos.y < _imgDims.y)
                    {
                        if (pt.Properties.IsLeftButtonPressed)
                            _layers[1].SetPixelBytes(_color, (int)pos.x, (int)pos.y, 1, 1);
                        else if (pt.Properties.IsRightButtonPressed)
                            _layers[1].SetPixelBytes(BLANK, (int)pos.x, (int)pos.y, 1, 1);
                    }
                    break;
            }
            _lastPointerPos = _pointerPos;
            RenderMain();
        }
        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _canvasDims = new Vector2(canvasControl.Size.Width, canvasControl.Size.Height);
            CanvasSizeText.Text = "Canvas Size: " + _canvasDims.x + "," + _canvasDims.y;
            CanvasSizeText.FontSize = 14;
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _pointerPressed = false;
            _pointerPos = new Vector2(0, 0);
            PointerPosText.Text = "Pointer Position: " + _pointerPos.x + "," + _pointerPos.y;
            PointerPosText.FontSize = 14;
        }

        private void CanvasControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _pointerPressed = true;

            var pt = e.GetCurrentPoint(canvasControl);
            _pointerPos.x = (int)pt.Position.X;
            _pointerPos.y = (int)pt.Position.Y;

            Vector2 pos;
            pos.x = (_pointerPos.x - _offset.x) / _scale;
            pos.y = (_pointerPos.y - _offset.y) / _scale;

            if (_toolState != ToolState.Pencil)
                return;
            if (pos.x >= 0 && pos.x < _imgDims.x && pos.y >= 0 && pos.y < _imgDims.y)
            {
                if (pt.Properties.IsLeftButtonPressed)
                    _layers[1].SetPixelBytes(_color, (int)pos.x, (int)pos.y, 1, 1);
                else if (pt.Properties.IsRightButtonPressed)
                    _layers[1].SetPixelBytes(BLANK, (int)pos.x, (int)pos.y, 1, 1);
            }

            RenderMain();
        }

        private void CanvasControl_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(canvasControl).Properties.MouseWheelDelta;
            var posCanvas = e.GetCurrentPoint(canvasControl).Position;
            _pointerPos = new Vector2(posCanvas.X, posCanvas.Y);
            var zoom = delta > 0 ? 2 : 0.5;
            _scale *= zoom;

            Vector2 pos;
            pos.x = (_pointerPos.x - _offset.x) / _scale;
            pos.y = (_pointerPos.y - _offset.y) / _scale;

            _offset.x = zoom * (pos.x - _scale * pos.x);
            _offset.y = zoom * (pos.y - _scale * pos.y);

            RenderMain();
        }

        private void CanvasControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _pointerPressed = false;
        }

        private void ResetPosition_Button(object sender, RoutedEventArgs e)
        {
            _offset = new Vector2(0, 0);
            _scale = 1;
            RenderMain();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb == null)
                return;
            string tag = rb.Tag.ToString();
            switch (tag)
            {
                case "Pan":
                    _toolState = ToolState.Pan;
                    break;
                case "Pencil":
                    _toolState = ToolState.Pencil;
                    break;
            }
        }
    }
}

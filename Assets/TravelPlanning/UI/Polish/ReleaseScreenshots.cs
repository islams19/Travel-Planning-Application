using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TravelPlanning.UI.Polish
{
    /// <summary>Optional graphical QA capture. A missing rendered frame fails instead of claiming visual validation.</summary>
    public sealed class ReleaseScreenshots : MonoBehaviour
    {
        public string OutputDirectory { get; set; }

        public async Task CaptureMatrixAsync(string name, CancellationToken token)
        {
            // Startup can report the correct window size while the splash renderer still owns the frame.
            for (int wait = 0; !UnityEngine.Rendering.SplashScreen.isFinished && wait < 600; wait++)
            {
                await Task.Delay(50, token);
            }

            if (!UnityEngine.Rendering.SplashScreen.isFinished)
            {
                throw new TimeoutException("The splash screen did not finish before capture.");
            }

            foreach (var size in new[]
            {
                new Vector2Int(1000, 700),
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1080),
                new Vector2Int(1920, 600)
            }

            )
            {
                token.ThrowIfCancellationRequested();
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                for (int wait = 0; (Screen.width != size.x || Screen.height != size.y) && wait < 100; wait++)
                {
                    await Task.Delay(30, token);
                }

                if (Screen.width != size.x || Screen.height != size.y)
                {
                    throw new InvalidOperationException("Window did not reach requested screenshot size " + size);
                }

                await Task.Delay(150, token);
                var captured = new TaskCompletionSource<bool>();
                StartCoroutine(CaptureFrame(name, size, captured));
                if (await Task.WhenAny(captured.Task, Task.Delay(10000, token)) != captured.Task)
                {
                    throw new TimeoutException("No rendered frame was available for capture.");
                }

                await captured.Task;
            }
        }

        private IEnumerator CaptureFrame(string name, Vector2Int size, TaskCompletionSource<bool> completed)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            // Wait for a second distinct rendered frame after startup or a resolution change.
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (!texture || texture.width != size.x || texture.height != size.y)
                {
                    throw new InvalidOperationException("Captured frame dimensions differ from the requested size.");
                }

                if (!HasVisiblePixels(texture))
                {
                    throw new InvalidOperationException("Captured frame is black; visual QA was not completed.");
                }

                Directory.CreateDirectory(OutputDirectory);
                string path = Path.Combine(OutputDirectory, name + "-" + size.x + "x" + size.y + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("RELEASE_CAPTURE " + path);
                completed.SetResult(true);
            }
            catch (Exception error)
            {
                completed.SetException(error);
            }
            finally
            {
                if (texture)
                {
                    Destroy(texture);
                }
            }
        }

        private static bool HasVisiblePixels(Texture2D texture)
        {
            // This application's white forms cannot legitimately produce an entirely black frame.
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y += Math.Max(1, texture.height / 16))
            {
                for (int x = 0; x < texture.width; x += Math.Max(1, texture.width / 16))
                {
                    var pixel = pixels[y * texture.width + x];
                    if (pixel.r > 3 || pixel.g > 3 || pixel.b > 3)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}

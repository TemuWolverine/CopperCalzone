using Android.Content.PM;
using Android.Views;
using AndroidX.Core.View;
using Avalonia;
using Avalonia.Android;
using CopperCalzone.Views;

namespace CopperCalzone.Android;

[Activity(
    Label = "CopperCalzone",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.FullUser,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 1. Initial measurement from Android system dimensions
        try
        {
            int resourceId = Resources?.GetIdentifier("status_bar_height", "dimen", "android") ?? 0;
            if (resourceId > 0 && Resources != null)
            {
                int pixelSize = Resources.GetDimensionPixelSize(resourceId);
                float density = Resources.DisplayMetrics?.Density ?? 1.0f;
                if (density > 0)
                {
                    double topDp = pixelSize / (double)density;
                    MainView.SetOsSafeInsets(0, topDp, 0, 0);
                }
            }
        }
        catch
        {
            // Fallback remains default 24 dp
        }

        // 2. Exact dynamic safe draw margin from Android OS WindowInsets
        if (Window?.DecorView != null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(Window.DecorView, new WindowInsetsHandler(this));
        }
    }

    private sealed class WindowInsetsHandler : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        private readonly Activity _activity;

        public WindowInsetsHandler(Activity activity)
        {
            _activity = activity;
        }

        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            try
            {
                var statusBars = insets.GetInsets(WindowInsetsCompat.Type.StatusBars());
                var displayCutout = insets.GetInsets(WindowInsetsCompat.Type.DisplayCutout());
                var navBars = insets.GetInsets(WindowInsetsCompat.Type.NavigationBars());

                float density = _activity.Resources?.DisplayMetrics?.Density ?? 1.0f;
                if (density <= 0) density = 1.0f;

                double safeTopDp = Math.Max(statusBars.Top, displayCutout.Top) / (double)density;
                double safeLeftDp = Math.Max(statusBars.Left, displayCutout.Left) / (double)density;
                double safeRightDp = Math.Max(statusBars.Right, displayCutout.Right) / (double)density;
                double safeBottomDp = navBars.Bottom / (double)density;

                MainView.SetOsSafeInsets(safeLeftDp, safeTopDp, safeRightDp, safeBottomDp);
            }
            catch
            {
            }

            return ViewCompat.OnApplyWindowInsets(v, insets);
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}

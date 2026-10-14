using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace CircleTools.SecretSanta.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	public App()
	{
		InitializeComponent();

		// Android: resize the WebView above the soft keyboard instead of panning the whole window up. Panning
		// pushed the fixed app bar under the status bar whenever a dialog field took focus (B6, 2026-10-14
		// review); with Resize the page keeps its app bar and the browser scrolls the focused field into view.
		// A no-op on the other platforms.
		On<Microsoft.Maui.Controls.PlatformConfiguration.Android>()
			.UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "CircleTools.SecretSanta.App" };
	}
}

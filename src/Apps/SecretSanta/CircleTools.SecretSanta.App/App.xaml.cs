// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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
		var window = new Window(new MainPage()) { Title = "CircleTools.SecretSanta.App" };

		// Resume / background for the sync triggers (ADR-0044 amendment 2026-10-15): coming back to the app
		// pulls what other devices changed, and the foreground poll stops while the app is in the background.
		window.Resumed += (_, _) => Platform.AppForegroundSignal.Raise(true);
		window.Stopped += (_, _) => Platform.AppForegroundSignal.Raise(false);
		return window;
	}
}

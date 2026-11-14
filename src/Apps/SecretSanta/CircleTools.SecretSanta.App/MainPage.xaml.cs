// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components.WebView.Maui;

namespace CircleTools.SecretSanta.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	/// <summary>
	/// The hybrid WebView, exposed so the Android host can route the system Back gesture/button into the
	/// app's own navigation history instead of finishing the activity.
	/// </summary>
	public BlazorWebView HybridWebView => blazorWebView;
}

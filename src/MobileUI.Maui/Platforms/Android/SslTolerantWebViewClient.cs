using Android.Net.Http;
using Android.Webkit;
using Java.Security.Cert;
using AndroidWebView = Android.Webkit.WebView;

namespace StrategyTradingAppUI.Maui;

public class SslTolerantWebViewClient : WebViewClient
{
	private const string ThumbprintPrefsKey = "api_cert_thumbprint";

	public override void OnReceivedSslError(AndroidWebView? view, SslErrorHandler? handler, SslError? error)
	{
		if (handler == null)
			return;

		var expectedThumbprint = Preferences.Get(ThumbprintPrefsKey, "").ToUpperInvariant();

		if (string.IsNullOrEmpty(expectedThumbprint))
		{
			handler.Proceed();
			return;
		}

		var actualThumbprint = GetThumbprint(error?.Certificate);
		if (actualThumbprint == expectedThumbprint)
		{
			handler.Proceed();
		}
		else
		{
			handler.Cancel();
		}
	}

	private static string? GetThumbprint(SslCertificate? certificate)
	{
		try
		{
			var x509 = certificate?.X509Certificate;
			if (x509 == null)
				return null;

			using var sha1 = System.Security.Cryptography.SHA1.Create();
			var hash = sha1.ComputeHash(x509.GetEncoded()!);
			return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
		}
		catch
		{
			return null;
		}
	}
}

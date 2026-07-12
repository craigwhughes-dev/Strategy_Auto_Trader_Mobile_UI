namespace MobileUI.Maui;

public static class ServiceHelper
{
	private static IServiceProvider? _serviceProvider;

	public static void Init(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public static T? GetService<T>() where T : class
	{
		return _serviceProvider?.GetService(typeof(T)) as T;
	}
}

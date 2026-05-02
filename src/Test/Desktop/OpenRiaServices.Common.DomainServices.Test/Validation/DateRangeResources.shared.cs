using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace TestDomainServices.Validation
{
    public static class DateRangeResources
    {
        private static CultureInfo _resourceCulture;
        private static ResourceManager _resourceManager;
        private static readonly object _lock = new object();

        private static readonly Dictionary<string, Dictionary<string, string>> _resourceFallback = 
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { 
                    "en", 
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "StartDateMustBeEarlierThanEndDate", "StartDate must be earlier than EndDate." },
                        { "StartDateRequired", "StartDate is required." },
                        { "EndDateRequired", "EndDate is required." }
                    }
                },
                { 
                    "zh", 
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "StartDateMustBeEarlierThanEndDate", "开始日期必须早于结束日期。" },
                        { "StartDateRequired", "开始日期是必填项。" },
                        { "EndDateRequired", "结束日期是必填项。" }
                    }
                }
            };

        public static CultureInfo Culture
        {
            get { return _resourceCulture ?? CultureInfo.CurrentUICulture; }
            set { _resourceCulture = value; }
        }

        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    lock (_lock)
                    {
                        if (_resourceManager == null)
                        {
                            try
                            {
                                var assembly = typeof(DateRangeResources).Assembly;
                                string resourceBaseName = GetResourceBaseName(assembly);
                                _resourceManager = new ResourceManager(resourceBaseName, assembly);
                            }
                            catch
                            {
                                _resourceManager = null;
                            }
                        }
                    }
                }
                return _resourceManager;
            }
        }

        private static string GetResourceBaseName(Assembly assembly)
        {
            string namespacePrefix = "TestDomainServices.Validation";
            string resourceName = "DateRangeResources";
            return $"{namespacePrefix}.{resourceName}";
        }

        private static string GetString(string key)
        {
            var culture = Culture;
            string result = null;

            var rm = ResourceManager;
            if (rm != null)
            {
                try
                {
                    result = rm.GetString(key, culture);
                }
                catch (MissingManifestResourceException)
                {
                    result = null;
                }
                catch (MissingSatelliteAssemblyException)
                {
                    result = null;
                }
            }

            if (result == null)
            {
                result = GetStringFromFallback(key, culture);
            }

            return result ?? key;
        }

        private static string GetStringFromFallback(string key, CultureInfo culture)
        {
            string language = culture.TwoLetterISOLanguageName;
            
            if (_resourceFallback.TryGetValue(language, out var resources) &&
                resources.TryGetValue(key, out var value))
            {
                return value;
            }

            if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            {
                if (_resourceFallback.TryGetValue("en", out var englishResources) &&
                    englishResources.TryGetValue(key, out var englishValue))
                {
                    return englishValue;
                }
            }

            return null;
        }

        public static string StartDateMustBeEarlierThanEndDate
        {
            get { return GetString("StartDateMustBeEarlierThanEndDate"); }
        }

        public static string StartDateRequired
        {
            get { return GetString("StartDateRequired"); }
        }

        public static string EndDateRequired
        {
            get { return GetString("EndDateRequired"); }
        }
    }
}

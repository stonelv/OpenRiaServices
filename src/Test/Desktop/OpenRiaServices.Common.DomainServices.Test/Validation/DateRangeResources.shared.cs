using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace TestDomainServices.Validation
{
    public static class DateRangeResources
    {
        private static CultureInfo _resourceCulture;
        private static ResourceManager _resourceManager;
        private static string _resourceBaseName;
        private static readonly object _lock = new object();

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
            if (_resourceBaseName != null)
            {
                return _resourceBaseName;
            }

            string[] resourceNames = assembly.GetManifestResourceNames();
            string targetSuffix = ".DateRangeResources.resources";

            foreach (string name in resourceNames)
            {
                if (name.EndsWith(targetSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    _resourceBaseName = name.Substring(0, name.Length - ".resources".Length);
                    return _resourceBaseName;
                }
            }

            _resourceBaseName = "TestDomainServices.Validation.DateRangeResources";
            return _resourceBaseName;
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

            if (result == null && !culture.Equals(CultureInfo.InvariantCulture))
            {
                rm = ResourceManager;
                if (rm != null)
                {
                    try
                    {
                        result = rm.GetString(key, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        result = null;
                    }
                }
            }

            return result ?? key;
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

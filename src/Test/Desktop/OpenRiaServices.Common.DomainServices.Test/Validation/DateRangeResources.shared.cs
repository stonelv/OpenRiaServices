using System;
using System.Globalization;

namespace TestDomainServices.Validation
{
    public static class DateRangeResources
    {
        private static CultureInfo _resourceCulture;

        public static CultureInfo Culture
        {
            get { return _resourceCulture ?? CultureInfo.CurrentUICulture; }
            set { _resourceCulture = value; }
        }

        private static string GetString(string key)
        {
            var culture = Culture;
            bool isChinese = culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

            return key switch
            {
                "StartDateMustBeEarlierThanEndDate" => isChinese 
                    ? "开始日期必须早于结束日期。" 
                    : "StartDate must be earlier than EndDate.",
                "StartDateRequired" => isChinese 
                    ? "开始日期是必填项。" 
                    : "StartDate is required.",
                "EndDateRequired" => isChinese 
                    ? "结束日期是必填项。" 
                    : "EndDate is required.",
                _ => key
            };
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

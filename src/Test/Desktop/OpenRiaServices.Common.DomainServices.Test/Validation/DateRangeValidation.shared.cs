using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace TestDomainServices.Validation
{
    public static class DateRangeValidator
    {
        public static ValidationResult ValidateDateRange(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var objectType = value.GetType();
            var startDateProperty = objectType.GetProperty("StartDate");
            var endDateProperty = objectType.GetProperty("EndDate");

            if (startDateProperty == null || endDateProperty == null)
            {
                return ValidationResult.Success;
            }

            var startDateValue = startDateProperty.GetValue(value);
            var endDateValue = endDateProperty.GetValue(value);

            if (startDateValue == null || endDateValue == null)
            {
                return ValidationResult.Success;
            }

            DateTime startDate = Convert.ToDateTime(startDateValue, CultureInfo.InvariantCulture);
            DateTime endDate = Convert.ToDateTime(endDateValue, CultureInfo.InvariantCulture);

            if (startDate >= endDate)
            {
                string errorMessage = DateRangeResources.StartDateMustBeEarlierThanEndDate;
                return new ValidationResult(errorMessage, new[] { "StartDate", "EndDate" });
            }

            return ValidationResult.Success;
        }
    }
}

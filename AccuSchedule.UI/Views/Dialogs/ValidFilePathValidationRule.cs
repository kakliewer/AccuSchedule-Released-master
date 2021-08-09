using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AccuSchedule.UI.Views.Dialogs
{
    public class ValidFilePathValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var invalidResponse = "Valid Path is Required.";
            var stringValue = (value ?? "").ToString();

            if (string.IsNullOrEmpty(stringValue)) 
                return new ValidationResult(false, invalidResponse);

            return !Directory.Exists(value.ToString())
                ? new ValidationResult(false, invalidResponse)
                : ValidationResult.ValidResult;
        }
    }
}

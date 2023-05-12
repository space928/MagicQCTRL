using Rug.Osc;
using System;
using System.Globalization;
using System.Windows.Controls;

namespace MagicQCTRLDesktopApp
{
    public class ValidOSCRule : ValidationRule
    {
        public ValidOSCRule()
        {
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            try
            {
                // A few special cases for empty OSC messages
                if (string.IsNullOrEmpty((string)value))
                    return ValidationResult.ValidResult;

                if((string)value == "/")
                    return ValidationResult.ValidResult;

                // Test the OSC message is parsable
                (string address, var args) = OSCMessageParser.ParseOSCMessage((string)value);
                _ = new OscMessage(address, args);
            }
            catch (Exception e)
            {
                return new ValidationResult(false, $"Invalid OSC message: {e.Message}");
            }

            return ValidationResult.ValidResult;
        }
    }
}

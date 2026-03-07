using System.Collections.Generic;
using System.Linq;

namespace SistemaCambio.Models
{
    public enum ValidationSeverity
    {
        Error,      // Bloquea la operación
        Warning,    // Permite continuar con confirmación
        Info        // Solo informativo
    }

    public class ValidationMessage
    {
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; } = "";
        public string? Details { get; set; }

        public static ValidationMessage Error(string message, string? details = null) =>
            new() { Severity = ValidationSeverity.Error, Message = message, Details = details };

        public static ValidationMessage Warning(string message, string? details = null) =>
            new() { Severity = ValidationSeverity.Warning, Message = message, Details = details };

        public static ValidationMessage Info(string message, string? details = null) =>
            new() { Severity = ValidationSeverity.Info, Message = message, Details = details };
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<ValidationMessage> Messages { get; set; } = new();

        public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);
        public bool HasWarnings => Messages.Any(m => m.Severity == ValidationSeverity.Warning);
        
        public List<ValidationMessage> Errors => 
            Messages.Where(m => m.Severity == ValidationSeverity.Error).ToList();
        
        public List<ValidationMessage> Warnings => 
            Messages.Where(m => m.Severity == ValidationSeverity.Warning).ToList();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult FromError(string message, string? details = null)
        {
            var result = new ValidationResult { IsValid = false };
            result.Messages.Add(ValidationMessage.Error(message, details));
            return result;
        }

        public void AddError(string message, string? details = null)
        {
            IsValid = false;
            Messages.Add(ValidationMessage.Error(message, details));
        }

        public void AddWarning(string message, string? details = null)
        {
            Messages.Add(ValidationMessage.Warning(message, details));
        }

        public void AddInfo(string message, string? details = null)
        {
            Messages.Add(ValidationMessage.Info(message, details));
        }
    }
}

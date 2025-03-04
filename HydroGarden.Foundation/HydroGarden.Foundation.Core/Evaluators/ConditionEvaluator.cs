using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using System.Linq.Expressions;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Evaluates conditions for component connections.
    /// </summary>
    public class ConditionEvaluator
    {
        private readonly IPersistenceService _persistenceService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConditionEvaluator"/> class.
        /// </summary>
        /// <param name="persistenceService">The persistence service for retrieving component properties.</param>
        public ConditionEvaluator(IPersistenceService persistenceService)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        }

        /// <summary>
        /// Evaluates a condition for a connection between source and target components.
        /// </summary>
        /// <param name="sourceId">The source component ID.</param>
        /// <param name="targetId">The target component ID.</param>
        /// <param name="condition">The condition expression to evaluate.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the condition is met; otherwise, false.</returns>
        public async Task<bool> EvaluateAsync(Guid sourceId, Guid targetId, string condition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true; // No condition means it passes

            // Parse the condition to identify property references
            var (deviceId, propertyName, op, value) = ParseCondition(condition);

            // Determine which device ID to use based on the reference
            Guid actualDeviceId;
            if (string.Equals(deviceId, "source", StringComparison.OrdinalIgnoreCase))
            {
                actualDeviceId = sourceId;
            }
            else if (string.Equals(deviceId, "target", StringComparison.OrdinalIgnoreCase))
            {
                actualDeviceId = targetId;
            }
            else if (Guid.TryParse(deviceId, out var parsedGuid))
            {
                actualDeviceId = parsedGuid;
            }
            else
            {
                // Default to source if no prefix specified
                actualDeviceId = sourceId;
                // If no device specified, use the property name as is
                propertyName = deviceId;
            }

            // Get the property value from the persistence service
            var propertyValue = await _persistenceService.GetPropertyAsync<object>(actualDeviceId, propertyName, ct);

            if (propertyValue == null)
                return false; // Property not found or null

            // Evaluate the condition based on the property type and operator
            return EvaluateCondition(propertyValue, op, value);
        }

        private (string deviceId, string propertyName, string op, string value) ParseCondition(string condition)
        {
            // Check if the condition is completely invalid
            if (!ContainsAnyOperator(condition))
            {
                throw new ArgumentException($"Invalid condition format (missing operator): {condition}");
            }

            // Handle the simplest case: PropertyName [operator] Value
            // Example: "Temperature > 25" or "source.Temperature > 25" or "target.IsActive == true"

            // Split by known operators
            foreach (var op in new[] { "==", "!=", ">=", "<=", ">", "<" })
            {
                if (condition.Contains(op))
                {
                    var parts = condition.Split(new[] { op }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var leftPart = parts[0].Trim();
                        var rightPart = parts[1].Trim();

                        // Check if there's a device prefix (source./target.)
                        string deviceId = "source"; // Default to source
                        string propertyName;

                        if (leftPart.StartsWith("source.", StringComparison.OrdinalIgnoreCase))
                        {
                            deviceId = "source";
                            propertyName = leftPart.Substring(7).Trim(); // Remove "source."
                        }
                        else if (leftPart.StartsWith("target.", StringComparison.OrdinalIgnoreCase))
                        {
                            deviceId = "target";
                            propertyName = leftPart.Substring(7).Trim(); // Remove "target."
                        }
                        else
                        {
                            // No device prefix, just a property name
                            propertyName = leftPart;
                        }

                        // Remove quotes from string values
                        if (rightPart.StartsWith("\"") && rightPart.EndsWith("\""))
                        {
                            rightPart = rightPart.Substring(1, rightPart.Length - 2);
                        }

                        return (deviceId, propertyName, op, rightPart);
                    }
                }
            }

            throw new ArgumentException($"Invalid condition format: {condition}");
        }

        private bool ContainsAnyOperator(string condition)
        {
            return condition.Contains("==") ||
                   condition.Contains("!=") ||
                   condition.Contains(">=") ||
                   condition.Contains("<=") ||
                   condition.Contains(">") ||
                   condition.Contains("<");
        }

        private bool EvaluateCondition(object propertyValue, string op, string valueStr)
        {
            // Handle different types of comparisons
            if (propertyValue is int intValue)
            {
                if (int.TryParse(valueStr, out var value))
                {
                    return EvaluateNumericCondition(intValue, op, value);
                }
            }
            else if (propertyValue is double doubleValue)
            {
                if (double.TryParse(valueStr, out var value))
                {
                    return EvaluateNumericCondition(doubleValue, op, value);
                }
            }
            else if (propertyValue is bool boolValue)
            {
                if (bool.TryParse(valueStr, out var value))
                {
                    return op == "==" ? boolValue == value : boolValue != value;
                }
            }
            else if (propertyValue is string strValue)
            {
                return op switch
                {
                    "==" => strValue == valueStr,
                    "!=" => strValue != valueStr,
                    _ => false
                };
            }

            return false; // If we can't evaluate, the condition fails
        }

        private bool EvaluateNumericCondition<T>(T propertyValue, string op, T value) where T : IComparable<T>
        {
            return op switch
            {
                "==" => propertyValue.CompareTo(value) == 0,
                "!=" => propertyValue.CompareTo(value) != 0,
                ">" => propertyValue.CompareTo(value) > 0,
                ">=" => propertyValue.CompareTo(value) >= 0,
                "<" => propertyValue.CompareTo(value) < 0,
                "<=" => propertyValue.CompareTo(value) <= 0,
                _ => false
            };
        }
    }
}
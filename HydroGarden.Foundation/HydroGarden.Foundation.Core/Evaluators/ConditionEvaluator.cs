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
            // Check for null or empty
            if (string.IsNullOrWhiteSpace(condition))
            {
                return (null, null, null, null);
            }

            // Try to parse the condition into deviceId.propertyName, operator, and value
            string[] operators = { ">=", "<=", "==", "!=", ">", "<", "=" };
            string foundOperator = null;
            int operatorIndex = -1;

            foreach (var op in operators)
            {
                int index = condition.IndexOf(op);
                if (index >= 0)
                {
                    // Found an operator
                    foundOperator = op;
                    operatorIndex = index;
                    break;
                }
            }

            // Handle case where no operator is found
            if (foundOperator == null || operatorIndex <= 0)
            {
                return (null, null, null, null);
            }

            string leftSide = condition.Substring(0, operatorIndex).Trim();
            string rightSide = condition.Substring(operatorIndex + foundOperator.Length).Trim();

            // Handle quoted strings - remove the surrounding quotes
            if (rightSide.StartsWith("\"") && rightSide.EndsWith("\"") && rightSide.Length >= 2)
            {
                rightSide = rightSide.Substring(1, rightSide.Length - 2);
            }

            // Parse the property reference to extract deviceId and propertyName
            string deviceId = "source"; // Default to source if no prefix
            string propertyName = leftSide;

            // Check if the property reference includes a deviceId prefix
            int dotIndex = leftSide.IndexOf('.');
            if (dotIndex > 0)
            {
                deviceId = leftSide.Substring(0, dotIndex).Trim();
                propertyName = leftSide.Substring(dotIndex + 1).Trim();
            }

            return (deviceId, propertyName, foundOperator, rightSide);
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
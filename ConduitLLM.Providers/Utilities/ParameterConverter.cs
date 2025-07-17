using System;
using System.Collections.Generic;
using System.Linq;

namespace ConduitLLM.Providers.Utilities
{
    /// <summary>
    /// Provides type-safe conversion utilities for mapping parameters from core models 
    /// to provider-specific formats while maintaining numeric precision.
    /// </summary>
    public static class ParameterConverter
    {
        /// <summary>
        /// Converts a nullable double to nullable float, maintaining as much precision as possible.
        /// </summary>
        /// <param name="value">The double value to convert.</param>
        /// <returns>The converted float value, or null if input is null.</returns>
        public static float? ToFloat(double? value)
        {
            if (!value.HasValue) return null;
            
            // Check for values outside float range
            if (value.Value > float.MaxValue) return float.MaxValue;
            if (value.Value < float.MinValue) return float.MinValue;
            
            return (float)value.Value;
        }
        
        /// <summary>
        /// Converts a dictionary of string to int (logit bias) to string to float format.
        /// </summary>
        /// <param name="logitBias">The logit bias dictionary with integer values.</param>
        /// <returns>A new dictionary with float values, or null if input is null.</returns>
        public static Dictionary<string, float>? ConvertLogitBias(Dictionary<string, int>? logitBias)
        {
            return logitBias?.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
        }
        
        /// <summary>
        /// Converts a stop parameter which might be a single string or list of strings
        /// to the format expected by OpenAI (object type).
        /// </summary>
        /// <param name="stop">The stop sequences list.</param>
        /// <returns>An object suitable for OpenAI API (string or string array).</returns>
        public static object? ConvertStopSequences(List<string>? stop)
        {
            if (stop == null || stop.Count == 0) return null;
            
            // OpenAI accepts either a string or array of strings
            return stop.Count == 1 ? stop[0] : stop;
        }
        
        /// <summary>
        /// Safely converts temperature values with validation.
        /// </summary>
        /// <param name="temperature">The temperature value (0.0 to 2.0).</param>
        /// <returns>The converted float value with bounds checking.</returns>
        public static float? ToTemperature(double? temperature)
        {
            if (!temperature.HasValue) return null;
            
            // Ensure temperature is within valid bounds (0.0 to 2.0)
            var value = Math.Max(0.0, Math.Min(2.0, temperature.Value));
            return (float)value;
        }
        
        /// <summary>
        /// Safely converts probability values (TopP, penalties) with validation.
        /// </summary>
        /// <param name="probability">The probability value.</param>
        /// <param name="minValue">Minimum allowed value (default -2.0 for penalties).</param>
        /// <param name="maxValue">Maximum allowed value (default 2.0 for penalties, 1.0 for TopP).</param>
        /// <returns>The converted float value with bounds checking.</returns>
        public static float? ToProbability(double? probability, double minValue = -2.0, double maxValue = 2.0)
        {
            if (!probability.HasValue) return null;
            
            // Ensure value is within bounds
            var value = Math.Max(minValue, Math.Min(maxValue, probability.Value));
            return (float)value;
        }
    }
}
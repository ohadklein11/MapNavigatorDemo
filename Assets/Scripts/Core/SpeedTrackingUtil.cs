using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Utility class for tracking player speed over time with outlier detection.
/// This utility maintains a rolling window of speed measurements and filters out
/// abnormal values to provide consistent speed tracking.
/// </summary>
public static class SpeedTrackingUtil
{
    /// <summary>
    /// Data structure to hold speed tracking information for a specific object
    /// </summary>
    public class SpeedTracker
    {
        private Queue<float> speedHistory;
        private Vector3 lastPosition;
        private float lastTime;
        private bool isInitialized;
        
        public int MaxHistoryFrames { get; set; } = 100;
        public float MaxReasonableSpeed { get; set; } = .8f; // Max speed in units per second
        public float MinFrameTime { get; set; } = 0.001f; // Minimum time between frames to consider
        
        public SpeedTracker(int maxHistoryFrames = 100, float maxReasonableSpeed = .8f)
        {
            speedHistory = new Queue<float>();
            MaxHistoryFrames = maxHistoryFrames;
            MaxReasonableSpeed = maxReasonableSpeed;
            isInitialized = false;
        }
        
        /// <summary>
        /// Updates the speed tracking with a new position
        /// </summary>
        /// <param name="currentPosition">Current position of the tracked object</param>
        /// <param name="deltaTime">Time since last update (use Time.deltaTime)</param>
        /// <returns>Current instantaneous speed, or -1 if filtered out as abnormal</returns>
        public float UpdateSpeed(Vector3 currentPosition, float deltaTime)
        {
            float currentTime = Time.time;
            
            if (!isInitialized)
            {
                lastPosition = currentPosition;
                lastTime = currentTime;
                isInitialized = true;
                return 0f;
            }
            
            float timeDiff = currentTime - lastTime;
            if (timeDiff < MinFrameTime)
            {
                return GetAverageSpeed(); // Return current average instead of updating
            }

            float distance = Vector3.Distance(currentPosition, lastPosition);
            float instantaneousSpeed = distance / timeDiff;
            
            // Check if speed is reasonable
            if (instantaneousSpeed <= MaxReasonableSpeed)
            {
                speedHistory.Enqueue(instantaneousSpeed);
                while (speedHistory.Count > MaxHistoryFrames)
                {
                    speedHistory.Dequeue();
                }
                lastPosition = currentPosition;
                lastTime = currentTime;
                
                return instantaneousSpeed;
            }
            else
            {
                // Speed is abnormal - don't update position/time, don't add to history
                if (Application.isEditor) // Only log in editor to avoid spam
                {
                    Debug.LogWarning($"SpeedTrackingUtil: Filtered out abnormal speed: {instantaneousSpeed:F2} units/sec " +
                                   $"(distance: {distance:F3}, time: {timeDiff:F4})");
                }
                return -1f; // Indicate filtered value
            }
        }
        
        /// <summary>
        /// Gets the average speed over the tracked history
        /// </summary>
        /// <returns>Average speed in units per second</returns>
        public float GetAverageSpeed()
        {
            if (speedHistory.Count == 0)
                return 0f;
            
            return speedHistory.Average();
        }
        
        /// <summary>
        /// Gets the current (most recent) speed measurement
        /// </summary>
        /// <returns>Most recent speed in units per second</returns>
        public float GetCurrentSpeed()
        {
            if (speedHistory.Count == 0)
                return 0f;
            
            return speedHistory.Last();
        }
        
        /// <summary>
        /// Clears all speed history
        /// </summary>
        public void Reset()
        {
            speedHistory.Clear();
            isInitialized = false;
        }
        
        /// <summary>
        /// Checks if the tracker has enough data for reliable speed calculation
        /// </summary>
        /// <param name="minMeasurements">Minimum number of measurements required</param>
        /// <returns>True if enough data is available</returns>
        public bool HasSufficientData(int minMeasurements = 5)
        {
            return speedHistory.Count >= minMeasurements;
        }
    }
    
    /// <summary>
    /// Creates a new speed tracker with default settings
    /// </summary>
    /// <param name="maxHistoryFrames">Maximum number of speed measurements to keep</param>
    /// <param name="maxReasonableSpeed">Maximum reasonable speed to accept (outlier threshold)</param>
    /// <returns>New SpeedTracker instance</returns>
    public static SpeedTracker CreateTracker(int maxHistoryFrames = 100, float maxReasonableSpeed = 0.8f)
    {
        return new SpeedTracker(maxHistoryFrames, maxReasonableSpeed);
    }
}
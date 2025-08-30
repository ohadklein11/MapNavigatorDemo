using UnityEngine;

/// <summary>
/// Utility class for detecting direction alignment between route segments and player movement.
/// Calculates angles between vectors to determine if the player is moving
/// in the same direction as the intended route.
/// </summary>
public static class DirectionDetectionUtil
{
    /// <summary>
    /// Calculates the angle between a route segment vector and a player movement vector.
    /// Both vectors are normalized before calculating the angle.
    /// </summary>
    /// <param name="routeSegmentStart">Start point of the route segment (point A)</param>
    /// <param name="routeSegmentEnd">End point of the route segment (point B)</param>
    /// <param name="playerMovementStart">Player position at start of movement (point C)</param>
    /// <param name="playerMovementEnd">Player position at end of movement (point D)</param>
    /// <returns>Angle in degrees between the two vectors (0-180 degrees)</returns>
    public static float CalculateDirectionAngle(Vector3 routeSegmentStart, Vector3 routeSegmentEnd, 
                                              Vector3 playerMovementStart, Vector3 playerMovementEnd)
    {
        // Calculate the route segment vector (b - a)
        Vector3 routeVector = routeSegmentEnd - routeSegmentStart;
        
        // Calculate the player movement vector (d - c)
        Vector3 playerVector = playerMovementEnd - playerMovementStart;
        
        // Check for zero-length vectors
        if (routeVector.magnitude < 0.001f)
        {
            Debug.LogWarning("DirectionDetectionUtil: Route segment vector has zero length");
            return 0f;
        }
        
        if (playerVector.magnitude < 0.001f)
        {
            Debug.LogWarning("DirectionDetectionUtil: Player movement vector has zero length");
            return 0f;
        }
        
        // Normalize the vectors
        Vector3 v = routeVector.normalized;
        Vector3 u = playerVector.normalized;
        
        // Calculate dot product
        float dotProduct = Vector3.Dot(v, u);
        
        // Clamp dot product to avoid floating point errors with acos
        dotProduct = Mathf.Clamp(dotProduct, -1f, 1f);
        
        // Calculate angle in radians and convert to degrees
        float angleRadians = Mathf.Acos(dotProduct);
        float angleDegrees = angleRadians * Mathf.Rad2Deg;
        
        return angleDegrees;
    }
}
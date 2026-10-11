namespace DazPose.Performer.HandGrip
{
    /// <summary>Source-neutral geometry contract for a cylindrical hand contact surface.</summary>
    public interface IGripTarget
    {
        GripFrame Evaluate(float position01);
    }

    /// <summary>Optional side-effect-free readiness contract for authored contact declarations.</summary>
    public interface IGripTargetReadiness
    {
        bool IsReadyForContact(bool runtime, out string reason);
    }

    /// <summary>
    /// Optional exact geometric certificate. A true result guarantees that, for every
    /// position in [0,1], Evaluate returns a frame whose center interpolates linearly between
    /// the returned endpoints and whose tangent, normal, binormal, and world-space radius are
    /// constant. Consumers may then certify the entire contact span from its endpoints.
    /// </summary>
    public interface IGripTargetStraightSpan : IGripTarget
    {
        bool TryGetStraightSpan(out GripFrame start, out GripFrame end);
    }
}

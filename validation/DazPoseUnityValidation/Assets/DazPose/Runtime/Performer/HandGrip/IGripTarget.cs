namespace DazPose.Performer.HandGrip
{
    /// <summary>Source-neutral geometry contract for a cylindrical hand contact surface.</summary>
    public interface IGripTarget
    {
        GripFrame Evaluate(float position01);
    }
}

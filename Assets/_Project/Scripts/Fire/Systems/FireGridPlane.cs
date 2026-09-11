namespace MRFireSafety.Fire.Systems
{
    /// <summary>
    /// Identifies the local-space plane that the cellular fire grid occupies. Fixing the plane
    /// explicitly prevents extinguishing agent from being projected onto the wrong axis pair when
    /// the virtual prop is rotated on the physical floor.
    /// </summary>
    public enum FireGridPlane
    {
        /// <summary>
        /// The grid lies on the local X/Y plane, which suits a vertical surface such as the front
        /// panel of a server rack or an electrical cabinet.
        /// </summary>
        LocalXY = 0,

        /// <summary>
        /// The grid lies on the local X/Z plane, which suits a horizontal surface such as a floor
        /// spill or the top face of a burning object.
        /// </summary>
        LocalXZ = 1
    }
}

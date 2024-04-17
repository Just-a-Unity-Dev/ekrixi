using Content.Shared.Materials;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._FTL.FTLPoints.Components;

[RegisterComponent]
public sealed partial class WarpDriveComponent : Component
{
    /// <summary>
    /// How much fuel does this drive currently have?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Fuel = 90;

    /// <summary>
    /// How much fuel is consumed per jump?
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int FuelPerJump = 30;

    /// <summary>
    /// Ships will begin warping when Charge reaches ChargeNeeded
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Charge;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int ChargeNeeded = 30;

    /// <summary>
    /// Is this drive charging?
    /// </summary>
    [DataField, ViewVariables]
    public bool Charging;

    /// <summary>
    /// The material to accept as fuel.
    /// </summary>
    [DataField("fuelMaterial", customTypeSerializer: typeof(PrototypeIdSerializer<MaterialPrototype>))]
    [ViewVariables(VVAccess.ReadWrite)]
    public string FuelMaterial = "Plasma";
}

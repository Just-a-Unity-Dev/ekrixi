using Content.Client.Computer;
using Content.Client.UserInterface.Controls;
using Content.Shared._FTL.ShipWeapons;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Map;
namespace Content.Client._FTL.ShipWeapons;

[GenerateTypedNameReferences]
public sealed partial class GunnerConsoleWindow : FancyWindow,
    IComputerWindow<GunnerConsoleBoundInterfaceState>
{
    public event Action<EntityCoordinates>? OnRadarClick;
    public event Action? OnFireClick;
    public event Action? OnEjectClick;

    public GunnerConsoleWindow()
    {
        RobustXamlLoader.Load(this);
        // IoCManager.InjectDependencies(this);

        WorldRangeChange(RadarScreen.WorldRange);
        RadarScreen.WorldRangeChanged += WorldRangeChange;
        RadarScreen.OnRadarClick += coordinates => { OnRadarClick?.Invoke(coordinates); };
        FireButton.OnButtonDown += _ => { OnFireClick?.Invoke(); };
        EjectButton.OnButtonDown += _ => { OnEjectClick?.Invoke(); };
    }

    private void WorldRangeChange(float value)
    {
        RadarRange.Text = $"{value:0}";
    }

    public void SetMatrix(EntityCoordinates? coordinates, Angle? angle)
    {
        RadarScreen.SetMatrix(coordinates, angle);
    }

    public void UpdateState(GunnerConsoleBoundInterfaceState scc)
    {
        // update the radar
        var radarState = new RadarConsoleBoundInterfaceState(
            scc.MaxRange,
            scc.Coordinates,
            scc.Angle,
            scc.Weapons
        );
        RadarScreen.UpdateState(radarState);

        // update ammo text
        AmmoCounter.Text = scc.CurrentAmmo <= 0 ? Loc.GetString("gunner-console-no-ammo") : $"{scc.CurrentAmmo}/{scc.MaxAmmo}";
        MaxRadarRange.Text = $"{scc.MaxRange:0}";
    }
}

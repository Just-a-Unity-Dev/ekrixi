using Content.Shared._FTL.ShipWeapons;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._FTL.ShipWeapons;

[UsedImplicitly]
public sealed class GunnerConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GunnerConsoleWindow? _window;
    private readonly IEntityManager _entityManager;

    public GunnerConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
    }

    protected override void Open()
    {
        base.Open();
        _window = new GunnerConsoleWindow();
        _window.OpenCentered();
        _window.OnClose += OnClose;
        _window.OnRadarClick += args =>
        {
            var msg = new RotateWeaponSendMessage(_entityManager.GetNetCoordinates(args));
            SendMessage(msg);
        };
        _window.OnFireClick += () =>
        {
            var msg = new PerformActionWeaponSendMessage(ShipWeaponAction.Fire);
            SendMessage(msg);
        };
        _window.OnEjectClick += () =>
        {
            var msg = new PerformActionWeaponSendMessage(ShipWeaponAction.Eject);
            SendMessage(msg);
        };
        _window.OnAutofireClick += () =>
        {
            var msg = new PerformActionWeaponSendMessage(ShipWeaponAction.ToggleAutofire);
            SendMessage(msg);
        };
        _window.OnChamberClick += () =>
        {
            var msg = new PerformActionWeaponSendMessage(ShipWeaponAction.Chamber);
            SendMessage(msg);
        };
    }

    private void OnClose()
    {
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not GunnerConsoleBoundInterfaceState cState)
            return;

        _window?.SetMatrix(_entityManager.GetCoordinates(cState.State.Coordinates), cState.State.Angle);
        _window?.UpdateState(cState);
    }
}

using System.Numerics;
using Content.Server.UserInterface;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._FTL.ShipWeapons;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._FTL.ShipWeapons;

public sealed class ShipWeaponsSystem : SharedShipWeaponsSystem
{
    // [Dependency] private readonly DeviceLinkSystem _deviceLinkSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly GunSystem _gunSystem = default!;
    [Dependency] private readonly SharedGunSystem _sharedGunSystem = default!;
    [Dependency] private readonly ContainerSystem _containerSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<GunnerConsoleComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        SubscribeLocalEvent<GunnerConsoleComponent, RotateWeaponSendMessage>(OnRotateWeapon);
        SubscribeLocalEvent<GunnerConsoleComponent, PerformActionWeaponSendMessage>(OnPerformActionWeapon);
        SubscribeLocalEvent<ShipWeaponComponent, ComponentInit>(ComponentInit);
    }

    private void ComponentInit(EntityUid uid, ShipWeaponComponent component, ComponentInit args)
    {
        _containerSystem.EnsureContainer<ContainerSlot>(uid, "gun_magazine");
        _containerSystem.EnsureContainer<ContainerSlot>(uid, "gun_chamber");
    }

    private void OnPerformActionWeapon(EntityUid uid, GunnerConsoleComponent component, PerformActionWeaponSendMessage args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var sourceComponent))
            return;

        var xform = Transform(uid);
        if (!xform.GridUid.HasValue)
            return;

        // Get every linked gun and fire it
        foreach (var (_, outputs) in sourceComponent.Outputs)
        {
            foreach (var entity in outputs)
            {
                if (!TryComp<GunComponent>(entity, out var gunComponent))
                    continue;
                if (!TryComp<ShipWeaponComponent>(entity, out var shipWeaponComponent))
                    continue;
                if (!TryComp<ChamberMagazineAmmoProviderComponent>(entity, out var chamberMagazineAmmoProviderComponent))
                    continue;
                if (entity == null)
                    continue; // i hate device linking
                var entXform = Transform(entity);

                if (!entXform.GridUid.HasValue)
                    continue;

                if (entXform.GridUid.Value != xform.GridUid.Value)
                    continue;

                switch (args.Action)
                {
                    case ShipWeaponAction.Fire:
                        if (!_gunSystem.CanShoot(gunComponent))
                            continue;

                        _gunSystem.AttemptShoot(entity, entity, gunComponent, shipWeaponComponent.Target);
                        break;
                    case ShipWeaponAction.Eject:
                        if (!_itemSlotsSystem.TryGetSlot(entity, "gun_magazine", out var itemSlot))
                            continue;
                        _itemSlotsSystem.TryEject(entity, itemSlot, null, out _);
                        break;
                    case ShipWeaponAction.Chamber:
                        _gunSystem.CycleCartridge(uid, chamberMagazineAmmoProviderComponent, null);
                        break;
                    case ShipWeaponAction.ToggleAutofire:
                        shipWeaponComponent.AutoFire = !shipWeaponComponent.AutoFire;
                        break;
                }
            }
        }
        UpdateUserInterface(uid, component);
    }

    private void OnRotateWeapon(EntityUid uid, GunnerConsoleComponent component, RotateWeaponSendMessage args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var sourceComponent))
            return;

        var xform = Transform(uid);
        if (!xform.GridUid.HasValue)
            return;

        // Get every linked gun and rotate it relative to it's position and the new target position
        foreach (var (_, outputs) in sourceComponent.Outputs)
        {
            foreach (var entity in outputs)
            {
                if (!TryComp<ShipWeaponComponent>(entity, out var shipWeaponComponent))
                    continue;
                if (entity == null)
                    continue;

                var entXform = Transform(entity);

                if (!entXform.GridUid.HasValue)
                    continue;

                if (entXform.GridUid.Value != xform.GridUid.Value)
                    continue;

                var entityXform = Transform(entity);

                // i didnt even need to use trig for this fml
                var angle = (_entityManager.GetCoordinates(args.Coordinates).ToMapPos(_entityManager, _transformSystem) - entityXform.MapPosition.Position).ToWorldAngle();
                shipWeaponComponent.DesiredAngle = angle;
                shipWeaponComponent.Target = _entityManager.GetCoordinates(args.Coordinates);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShipWeaponComponent>();
        while (query.MoveNext(out var entity, out var weaponComponent))
        {
            var xform = Transform(entity);
            xform.LocalRotation = Angle.Lerp(xform.LocalRotation, weaponComponent.DesiredAngle, 0.1f);

            if (!TryComp<GunComponent>(entity, out var gunComponent))
                continue;

            if (_gunSystem.CanShoot(gunComponent) && weaponComponent.AutoFire)
            {
                _gunSystem.AttemptShoot(entity, entity, gunComponent, weaponComponent.Target);
            }
        }
    }

    private void UpdateUserInterface(EntityUid uid, GunnerConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        var totalAmmo = 0;
        var remainingAmmo = 0;

        var consoleTransform = Transform(uid);
        if (!TryComp<RadarConsoleComponent>(uid, out var radar))
            return;

        // Render every gun as a DockingInterfaceState
        if (TryComp<DeviceLinkSourceComponent>(uid, out var sourceComponent))
        {
            // yeah we loop through everything
            foreach (var (_, outputs) in sourceComponent.Outputs)
            {
                foreach (var entity in outputs)
                {
                    // we cant really do ammo count if it has no guns, so...
                    if (!TryComp<GunComponent>(entity, out _))
                        continue;

                    var ev = new GetAmmoCountEvent();
                    RaiseLocalEvent(entity, ref ev);

                    totalAmmo += ev.Capacity;
                    remainingAmmo += ev.Count;
                }
            }
        }

        var range = radar?.MaxRange ?? SharedRadarConsoleSystem.DefaultMaxRange;
        var state = new GunnerConsoleBoundInterfaceState(
            remainingAmmo,
            totalAmmo,
            new NavInterfaceState(range, _entityManager.GetNetCoordinates(consoleTransform.Coordinates), consoleTransform.LocalRotation, new Dictionary<NetEntity, List<DockingPortState>>())
        );

        _userInterface.SetUiState(uid, ShipWeaponTargetingUiKey.Key, state);

    }

    private void OnToggleInterface(EntityUid uid, GunnerConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        _userInterface.TryOpenUi(uid, ShipWeaponTargetingUiKey.Key, args.Actor);
        UpdateUserInterface(uid, component);
    }
}

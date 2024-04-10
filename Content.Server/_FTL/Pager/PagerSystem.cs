using Content.Server.Administration;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Popups;
using Content.Shared._FTL.Pager;
using Content.Shared.DeviceNetwork;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._FTL.Pager;

/// <summary>
/// This handles...
/// </summary>
public sealed class PagerSystem : EntitySystem
{
    [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialogSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public const string NetCmdSend = "pager_beep";
    public const string NetMessage = "pager_message";

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PagerReceiverComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
        SubscribeLocalEvent<PagerReceiverComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<PagerReceiverComponent, ActivateInWorldEvent>(OnActivated);

        SubscribeLocalEvent<PagerActionsComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnActivated(EntityUid uid, PagerReceiverComponent component, ActivateInWorldEvent args)
    {
        _audioSystem.PlayPvs(component.BeepSound, uid);

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (component.Paged)
        {
            _audioSystem.Stop(component.PlayingStream);
            component.Paged = false;
            _appearanceSystem.SetData(uid, PagerVisualLayers.Receiving, false);
        }
        else
        {
            TryPage(uid, actor, null);
        }
    }

    private void OnExamine(EntityUid uid, PagerReceiverComponent component, ExaminedEvent args)
    {
        if (!TryComp<DeviceNetworkComponent>(uid, out var networkComponent))
            return;
        args.AddMarkup(Loc.GetString("examine-pager-device-id", ("addr", networkComponent.Address)));
        if (component.PagerMessage != "")
            args.AddMarkup(Loc.GetString("examine-pager-read-message", ("msg", component.PagerMessage)));
    }

    private void OnGetVerbs(EntityUid uid, PagerActionsComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (args is { CanAccess: false, CanInteract: false })
            return;
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("verb-popup-page-person"),
            Act = () =>
            {
                TryPage(uid, actor, component);
            }
        });

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("verb-popup-set-alias"),
            Priority = 5,
            Act = () =>
            {
                _quickDialogSystem.OpenDialog(actor.PlayerSession, Loc.GetString("window-alias-menu-title"), Loc.GetString("window-alias-menu-address"), Loc.GetString("window-alias-menu-name"), (string toPage, string alias) =>
                {
                    component.Aliases[alias] = toPage;
                    _popupSystem.PopupEntity(Loc.GetString("popup-pager-set-alias"), uid);
                });
            }
        });
    }

    private void TryPage(EntityUid uid, ActorComponent actor, PagerActionsComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        _quickDialogSystem.OpenDialog(actor.PlayerSession, Loc.GetString("window-page-menu-title"), Loc.GetString("window-page-menu-address"), Loc.GetString("window-page-menu-message"), (string toPage, string message) =>
        {
            var payload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = NetCmdSend,
                [NetMessage] = message
            };

            // component.Aliases.TryGetValue(toPage, out var addr);
            // addr ??= toPage;
            var addr = toPage;
            if (component.Aliases.TryGetValue(toPage, out var alias))
                addr = alias;

            _deviceNetworkSystem.QueuePacket(uid, addr, payload, 2208);

            _popupSystem.PopupEntity(Loc.GetString("popup-pager-open-paging"), uid);
        });
    }

    private void OnPacketReceived(EntityUid uid, PagerReceiverComponent component, DeviceNetworkPacketEvent args)
    {
        if (component.Paged)
            return; // already being paged by someone

        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out var command))
            return;
        if (command != null && (string) command != NetCmdSend)
            return;
        if (args.Data.TryGetValue(NetMessage, out var message))
        {
            if (message != null)
            {
                var msg = (string) message;
                component.PagerMessage = msg;
                if (component.PagerMessage.Length > 14)
                {
                    component.PagerMessage = component.PagerMessage[..14];
                }
            }
        }

        component.Paged = true;
        component.PlayingStream = _audioSystem.PlayPvs(component.PagingSound, uid, AudioParams.Default.WithLoop(true)).Value.Entity;
        _popupSystem.PopupEntity(Loc.GetString("popup-pager-being-paged"), uid);
        _appearanceSystem.SetData(uid, PagerVisualLayers.Receiving, true);
    }
}

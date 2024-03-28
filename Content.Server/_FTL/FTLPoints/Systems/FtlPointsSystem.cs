using System.Linq;
using System.Numerics;
using Content.Server._FTL.FTLPoints.Components;
using Content.Server._FTL.FTLPoints.Effects;
using Content.Server._FTL.FTLPoints.Prototypes;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.UserInterface;
using Content.Shared._FTL.FtlPoints;
using Content.Shared.Dataset;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Parallax;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Salvage;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._FTL.FTLPoints.Systems;

/// <summary>
/// This handles the generation of FTL points
/// </summary>
public sealed partial class FtlPointsSystem : SharedFtlPointsSystem
{
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ShuttleConsoleSystem _consoleSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StarMapComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<StarmapConsoleComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        SubscribeLocalEvent<StarmapConsoleComponent, WarpToStarMessage>(OnWarpToStarMessage);
        SubscribeLocalEvent<WarpDriveComponent, InteractHandEvent>(OnDriveInteractHand);
        SubscribeLocalEvent<WarpDriveComponent, ExaminedEvent>(OnDriveExamineEvent);
    }

    /// <summary>
    /// Generates a float within minimum and maximum, with a 50% chance of being negative.
    /// </summary>
    /// <param name="minRadius"></param>
    /// <param name="maxRadius"></param>
    /// <returns></returns>
    public float GenerateVectorWithRandomRadius(float minRadius, float maxRadius)
    {
        return _random.NextFloat(minRadius, maxRadius) * (_random.Prob(0.5f) ? -1 : 1);
    }

    /// <summary>
    /// Generates a random sector.
    /// </summary>
    /// <param name="maxStars">How many stars in the newest generation until we must forcibly stop it?</param>
    /// <param name="startingPoint"></param>
    /// <param name="clear">Should we clear all previous stars</param>
    /// <param name="deleteStars">Should we delete all those previous stars?</param>
    /// <returns>The MapId of the central trade station.</returns>
    public MapId GenerateSector(int maxStars, MapId? startingPoint, bool clear = false, bool deleteStars = false)
    {
        var centerStation = startingPoint ?? GeneratePoint(_prototypeManager.Index<FtlPointPrototype>("StationPoint"));

        if (clear)
            RemoveAllStars(deleteStars);

        var latestGeneration = new List<Vector2>
        {
            Vector2.Zero
        };
        TryAddPoint(centerStation, new Vector2(0,0), Loc.GetString("starmap-center-station"));

        var starsCreated = 0;

        Log.Info("Generating sector.");

        while (starsCreated <= maxStars)
        {
            var toIter = latestGeneration.ToList();
            latestGeneration.Clear();
            var first = true;
            foreach (var origin in toIter)
            {
                var branches = _random.Next(1, 3);

                for (var i = 0; i < branches; i++)
                {
                    var prototype = _prototypeManager.Index<FtlPointPrototype>(_prototypeManager.Index<WeightedRandomPrototype>("FTLPoints").Pick());
                    Log.Info($"Picked {prototype.ID} as point type.");
                    if (_random.Prob(prototype.Probability) || first) // if its the first star then just set it
                    {
                        var mapId = GeneratePoint(prototype);
                        var mapUid = _mapManager.GetMapEntityId(mapId);
                        var position = new Vector2(
                            origin.X + GenerateVectorWithRandomRadius(3, 5),
                            origin.Y + GenerateVectorWithRandomRadius(3, 5)
                        );
                        if (first)
                        {
                            position = new Vector2(
                                origin.X + _random.NextFloat(6, 8.5f),
                                origin.Y + _random.NextFloat(6, 8.5f)
                            );
                        }

                        TryAddPoint(mapId, position, MetaData(mapUid).EntityName);
                        _mapManager.SetMapPaused(mapId, false);
                        latestGeneration.Add(position);
                    }
                    starsCreated++;
                }

                if (first)
                {
                    first = false;
                }
            }
        }

        for (var i = 0; i < 3; i++)
        {
            var origin = _random.Pick(latestGeneration);
            var prototype = _prototypeManager.Index<FtlPointPrototype>("WarpPoint");
            var mapId = GeneratePoint(prototype);
            var mapUid = _mapManager.GetMapEntityId(mapId);
            var position = new Vector2(
                origin.X + GenerateVectorWithRandomRadius(5, 7),
                origin.Y + GenerateVectorWithRandomRadius(5, 7)
            );
            TryAddPoint(mapId, position, MetaData(mapUid).EntityName);
            latestGeneration.Add(position);
            starsCreated++;
        }

        Log.Debug("Generated a brand new sector.");

        return centerStation;
    }

    /// <summary>
    /// Generates a temporary disposable FTL point.
    /// </summary>
    public MapId GeneratePoint(FtlPointPrototype prototype)
    {
        // create map

        var mapId = _mapManager.CreateMap();
        _mapManager.SetMapPaused(mapId, true);
        var mapUid = _mapManager.GetMapEntityId(mapId);

        // make it ftlable
        EnsureComp<FTLDestinationComponent>(mapUid);
        _metaDataSystem.SetEntityName(mapUid, $"[{Loc.GetString(prototype.Tag)}] {
            SharedSalvageSystem.GetFTLName(_prototypeManager.Index<DatasetPrototype>("names_borer"), _random.Next())}");
        _consoleSystem.RefreshShuttleConsoles();

        // add parallax
        var parallaxes = new[]
        {
            "AspidParallax",
            "KettleStation",
            "Default",
            "Blank",
            "BagelStation",
            "Blue_Nebula_01",
            "Blue_Nebula_02",
            "Blue_Nebula_03",
            "Blue_Nebula_04",
            "Green_Nebula_01",
            "Green_Nebula_02",
            "Green_Nebula_03",
            "Green_Nebula_04",
            "Green_Nebula_06",
            "Green_Nebula_07",
            "Green_Nebula_08",
            "Purple_Nebula_01",
            "Purple_Nebula_02",
            "Purple_Nebula_03",
            "Purple_Nebula_04",
            "Purple_Nebula_05",
            "Purple_Nebula_08"
        };
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = _random.Pick(parallaxes);

        // spawn the stuff
        foreach (var effect in prototype.FtlPointEffects)
        {
            if (_random.Prob(effect.Probability))
            {
                effect.Effect(new FtlPointEffect.FtlPointEffectArgs(mapUid, mapId, _entManager, _mapManager));
            }
        }

        // Add all components required by the prototype
        if (prototype.TickComponents == null)
            return mapId;

        foreach (var entry in prototype.TickComponents.Values)
        {
            if (HasComp(mapUid, entry.Component.GetType()))
                continue;

            var comp = (Component) _serializationManager.CreateCopy(entry.Component, notNullableOverride: true);
            comp.Owner = mapUid;
            EntityManager.AddComponent(mapUid, comp);
        }

        return mapId;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        DriveUpdate(frameTime);
    }
}

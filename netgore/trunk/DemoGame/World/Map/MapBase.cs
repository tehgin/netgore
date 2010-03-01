using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using DemoGame.DbObjs;
using log4net;
using Microsoft.Xna.Framework;
using NetGore;
using NetGore.Collections;
using NetGore.IO;

namespace DemoGame
{
    /// <summary>
    /// Event handler for basic events from the MapBase.
    /// </summary>
    /// <param name="map">MapBase that the event came from.</param>
    public delegate void MapBaseEventHandler(MapBase map);

    /// <summary>
    /// Base map class
    /// </summary>
    public abstract class MapBase : IMap, IMapTable
    {
        static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The suffix used for map files. Does not include the period prefix.
        /// </summary>
        public const string MapFileSuffix = "xml";

        const string _dynamicEntitiesNodeName = "DynamicEntities";

        /// <summary>
        /// The number of additional pixels to search in each direction around an entity when finding
        /// a valid place to put the entity. Larger values will allow for a better chance of finding a valid position,
        /// but will allow the entity to warp farther away from their initial position.
        /// </summary>
        const int _findValidPlacementPadding = 128;

        const string _headerNodeHeightKey = "Height";
        const string _headerNodeMusicKey = "Music";
        const string _headerNodeName = "Header";
        const string _headerNodeNameKey = "Name";
        const string _headerNodeWidthKey = "Width";
        const string _headerNodeIndoorsKey = "Indoors";
        const string _miscNodeName = "Misc";
        const string _rootNodeName = "Map";
        const string _wallsNodeName = "Walls";

        /// <summary>
        /// Collection of DynamicEntities on this map.
        /// </summary>
        readonly DArray<DynamicEntity> _dynamicEntities = new DArray<DynamicEntity>(true);

        /// <summary>
        /// List of entities in the map
        /// </summary>
        readonly List<Entity> _entities = new List<Entity>();

        /// <summary>
        /// Interface used to get the time
        /// </summary>
        readonly IGetTime _getTime;

        /// <summary>
        /// Index of the map
        /// </summary>
        readonly MapIndex _mapIndex;

        readonly ISpatialCollection _spatialCollection;

        readonly List<IUpdateableEntity> _updateableEntities = new List<IUpdateableEntity>();

        /// <summary>
        /// StopWatch used to update the map
        /// </summary>
        readonly Stopwatch _updateStopWatch = new Stopwatch();

        /// <summary>
        /// Height of the map in pixels
        /// </summary>
        float _height = float.MinValue;

        /// <summary>
        /// If the map is actively updating (set to false to "pause" the physics)
        /// </summary>
        bool _isUpdating = true;

        /// <summary>
        /// Name of the map
        /// </summary>
        string _name = string.Empty;

        /// <summary>
        /// Width of the map in pixels
        /// </summary>
        float _width = float.MinValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MapBase"/> class.
        /// </summary>
        /// <param name="mapIndex">Index of the map.</param>
        /// <param name="getTime">Interface used to get the time.</param>
        protected MapBase(MapIndex mapIndex, IGetTime getTime)
        {
            if (getTime == null)
                throw new ArgumentNullException("getTime");

            _getTime = getTime;
            _mapIndex = mapIndex;

            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            _spatialCollection = CreateSpatialManager();
            // ReSharper restore DoNotCallOverridableMethodsInConstructor

            _updateStopWatch.Start();
        }

        /// <summary>
        /// Notifies listeners that the Map has been saved.
        /// </summary>
        public event MapBaseEventHandler Saved;

        /// <summary>
        /// Gets an IEnumerable of all the DynamicEntities on the Map.
        /// </summary>
        public IEnumerable<DynamicEntity> DynamicEntities
        {
            get { return _dynamicEntities; }
        }

        /// <summary>
        /// Gets the index of the map.
        /// </summary>
        public MapIndex Index
        {
            get { return _mapIndex; }
        }

        /// <summary>
        /// Gets or sets if this map represents the map of an area that is indoors.
        /// </summary>
        public bool Indoors { get; set; }

        /// <summary>
        /// Gets or sets if the map is updating every frame
        /// </summary>
        public bool IsUpdating
        {
            get { return _isUpdating; }
            set
            {
                if (_isUpdating != value)
                {
                    _isUpdating = value;
                    if (_isUpdating)
                        _updateStopWatch.Start();
                    else
                        _updateStopWatch.Stop();
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the music to play for the map, or empty or null if there is no music.
        /// </summary>
        public string Music { get; set; }

        /// <summary>
        /// Adds a DynamicEntity to the Map, using the pre-determined unique index.
        /// </summary>
        /// <param name="entity">DynamicEntity to add to the Map.</param>
        /// <param name="mapEntityIndex">Unique index to assign to the DynamicEntity.</param>
        public virtual void AddDynamicEntity(DynamicEntity entity, MapEntityIndex mapEntityIndex)
        {
            if (entity == null)
                throw new ArgumentNullException("entity");

            Debug.Assert(!_dynamicEntities.Contains(entity), "DynamicEntity is already in the DynamicEntity list!");

            if (_dynamicEntities.CanGet((int)mapEntityIndex))
            {
                var existingDE = _dynamicEntities[(int)mapEntityIndex];
                if (existingDE != null)
                {
                    Debug.Fail("A DynamicEntity already exists at this MapEntityIndex!");
                    RemoveEntity(existingDE);
                }
            }

            entity.MapEntityIndex = mapEntityIndex;
            _dynamicEntities[(int)mapEntityIndex] = entity;

            AddEntityFinish(entity);
        }

        /// <summary>
        /// Adds an Entity to the map.
        /// </summary>
        /// <param name="entity">Entity to add to the map.</param>
        public virtual void AddEntity(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException("entity");

            // Assign a DynamicEntity its unique map index
            DynamicEntity dynamicEntity;
            if ((dynamicEntity = entity as DynamicEntity) != null)
            {
                Debug.Assert(!_dynamicEntities.Contains(dynamicEntity), "DynamicEntity is already in the DynamicEntity list!");
                dynamicEntity.MapEntityIndex = new MapEntityIndex(_dynamicEntities.Insert(dynamicEntity));
            }

            // Finish adding the Entity
            AddEntityFinish(entity);
        }

        void AddEntityFinish(Entity entity)
        {
            AddEntityToEntityList(entity);
            ForceEntityInMapBoundaries(entity);
        }

        /// <summary>
        /// Adds an entity to the entity list, which in turn adds the entity to the entity grid
        /// and creates the hooks to the entity's events.
        /// </summary>
        /// <param name="entity">Entity to add to the entity list</param>
        void AddEntityToEntityList(Entity entity)
        {
            // When in debug build, ensure we do not add an entity that is already added
            Debug.Assert(!_entities.Contains(entity), "entity already in the map's entity list!");

            // Add to the one-dimensional entity list
            _entities.Add(entity);
            if (entity is IUpdateableEntity)
                _updateableEntities.Add((IUpdateableEntity)entity);

            // Check for the IUpdateableMapReference interface
            if (entity is IUpdateableMapReference)
                ((IUpdateableMapReference)entity).Map = this;

            // Also add the entity to the grid
            Spatial.Add(entity);

            // Add the event hooks
            entity.Disposed += Entity_Disposed;

            // Allow for additional processing
            EntityAdded(entity);
        }

        void CheckCollisionAgainstEntities(Entity entity)
        {
            // Get the entities we have a rectangular collision with
            var collisionSources = Spatial.GetMany<Entity>(entity, x => !(x is WallEntityBase));

            foreach (var other in collisionSources)
            {
                var displacement = SpatialHelper.MTD(entity, other);
                if (displacement != Vector2.Zero)
                {
                    entity.CollideInto(other, displacement);
                    other.CollideFrom(entity, displacement);
                }
            }
        }

        void CheckCollisionsAgainstWalls(Entity entity)
        {
            if (!entity.CollidesAgainstWalls)
                return;

            // Get the entities we have a rectangular collision with
            var collisionSources = Spatial.GetMany<WallEntityBase>(entity);

            // Do real collision detection on the entities, and handle it if the collision test passes
            foreach (var wall in collisionSources)
            {
                // Get the displacement vector if the two entities collided
                var displacement = SpatialHelper.MTD(entity, wall);

                // If there is a displacement value, forward it to the collision notifiers
                if (displacement != Vector2.Zero)
                    wall.HandleCollideInto(entity, displacement);
            }
        }

        /// <summary>
        /// Describes how to create the <see cref="ISpatialCollection"/> to be used by the map. This can be overridden
        /// in the derived class to provide a different <see cref="ISpatialCollection"/> implementation.
        /// </summary>
        /// <param name="classTypeTree">The class type tree.</param>
        /// <returns>The <see cref="ISpatialCollection"/> to be used by the map.</returns>
        protected virtual ISpatialCollection CreateSpatialCollection(ClassTypeTree classTypeTree)
        {
            if (classTypeTree.Type == typeof(WallEntityBase))
                return new StaticGridSpatialCollection();
            else
                return new DynamicGridSpatialCollection();
        }

        /// <summary>
        /// Creates the <see cref="ISpatialCollection"/> to be used for the spatial objects on the map.
        /// </summary>
        /// <returns>The <see cref="ISpatialCollection"/> to be used for the spatial objects on the map.</returns>
        protected virtual ISpatialCollection CreateSpatialManager()
        {
            return new SpatialManager(GetSpatialTypes(), CreateSpatialCollection);
        }

        /// <summary>
        /// When overridden in the derived class, creates a new WallEntityBase instance.
        /// </summary>
        /// <returns>WallEntityBase that is to be used on the map.</returns>
        protected abstract WallEntityBase CreateWall(IValueReader r);

        /// <summary>
        /// Handles when an Entity is disposed while still on the map.
        /// </summary>
        /// <param name="entity"></param>
        void Entity_Disposed(Entity entity)
        {
            RemoveEntity(entity);
        }

        /// <summary>
        /// When overridden in the derived class, allows for additional processing on Entities added to the map.
        /// This is called after the Entity has finished being added to the map.
        /// </summary>
        /// <param name="entity">Entity that was added to the map.</param>
        protected virtual void EntityAdded(Entity entity)
        {
        }

        /// <summary>
        /// When overridden in the derived class, allows for additional processing on Entities removed from the map.
        /// This is called after the Entity has finished being removed from the map.
        /// </summary>
        /// <param name="entity">Entity that was removed from the map.</param>
        protected virtual void EntityRemoved(Entity entity)
        {
        }

        /// <summary>
        /// Checks if an Entity is in the map's boundaries and, if it is not, moves the Entity into the map's boundaries.
        /// </summary>
        /// <param name="entity">Entity to check.</param>
        void ForceEntityInMapBoundaries(Entity entity)
        {
            var min = entity.Position;
            var max = entity.Max;

            if (min.X < 0)
                min.X = 0;
            if (min.Y < 0)
                min.Y = 0;
            if (max.X >= Width)
                min.X = Width - entity.Size.X;
            if (max.Y >= Height)
                min.Y = Height - entity.Size.Y;

            if (min != entity.Position)
                entity.Teleport(min);
        }

        /// <summary>
        /// Gets the DynamicEntity with the specified index.
        /// </summary>
        /// <param name="mapEntityIndex">Index of the DynamicEntity to get.</param>
        /// <returns>The DynamicEntity with the specified <paramref name="mapEntityIndex"/>, or null if
        /// no <see cref="DynamicEntity"/> was found..</returns>
        public DynamicEntity GetDynamicEntity(MapEntityIndex mapEntityIndex)
        {
            if (!_dynamicEntities.CanGet((int)mapEntityIndex))
                return null;

            try
            {
                return _dynamicEntities[(int)mapEntityIndex];
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the DynamicEntity with the specified index.
        /// </summary>
        /// <typeparam name="T">Type of DynamicEntity to get.</typeparam>
        /// <param name="mapEntityIndex">Index of the DynamicEntity to get.</param>
        /// <returns>The DynamicEntity with the specified <paramref name="mapEntityIndex"/>, or null if
        /// no <see cref="DynamicEntity"/> was found.</returns>
        public T GetDynamicEntity<T>(MapEntityIndex mapEntityIndex) where T : DynamicEntity
        {
            var dynamicEntity = GetDynamicEntity(mapEntityIndex);
            if (dynamicEntity == null)
                return null;

            var casted = dynamicEntity as T;

            if (casted == null)
            {
                const string errmsg = "DynamicEntity found, but was of type `{0}`, not type `{1}` like expected!";
                Debug.Fail(string.Format(errmsg, dynamicEntity.GetType(), typeof(T)));
                if (log.IsErrorEnabled)
                    log.ErrorFormat(errmsg, dynamicEntity.GetType(), typeof(T));
            }

            return casted;
        }

        /// <summary>
        /// Allows an entity to be grabbed by index. Intended only for performance cases where iterating
        /// through the entire entity set would not be optimal (such as comparing the entities to each other).
        /// </summary>
        /// <param name="index">Index of the entity to get</param>
        /// <returns>Entity for the given index, or null if invalid</returns>
        protected Entity GetEntity(int index)
        {
            // Ensure the index is valid
            if (index < 0 || index >= _entities.Count)
            {
                Debug.Fail("Invalid index.");
                return null;
            }

            // Get the entity at the index
            return _entities[index];
        }

        /// <summary>
        /// Gets an IEnumerable of the path of all the map files in the given ContentPaths.
        /// </summary>
        /// <param name="path">ContentPaths to load the map files from.</param>
        /// <returns>An IEnumerable of all the map files in <paramref name="path"/>.</returns>
        public static IEnumerable<string> GetMapFiles(ContentPaths path)
        {
            // Get all the files
            var allFiles = Directory.GetFiles(path.Maps);

            // Select only valid map files
            var mapFiles = allFiles.Where(IsValidMapFile);

            return mapFiles;
        }

        /// <summary>
        /// Gets the next free map index.
        /// </summary>
        /// <param name="path">ContentPaths containing the maps.</param>
        /// <returns>Next free map index.</returns>
        public static MapIndex GetNextFreeIndex(ContentPaths path)
        {
            var mapFiles = GetMapFiles(path);

            // Get the used map indices
            var usedIndices = new List<MapIndex>(mapFiles.Count());
            foreach (var file in mapFiles)
            {
                MapIndex o;
                if (TryGetIndexFromPath(file, out o))
                    usedIndices.Add(o);
            }

            usedIndices.Sort();

            // Check every map index starting at 1, returning the first free value found
            var expected = 1;
            for (var i = 0; i < usedIndices.Count; i++)
            {
                if ((int)usedIndices[i] != expected)
                    return new MapIndex(expected);

                expected++;
            }

            return new MapIndex(expected);
        }

        /// <summary>
        /// Gets the possible positions to place the given <see cref="ISpatial"/> around a <see cref="Rectangle"/>.
        /// </summary>
        /// <param name="spatial">The <see cref="ISpatial"/> to place around.</param>
        /// <param name="r">The <see cref="Rectangle"/> describing the area to be placed.</param>
        /// <returns>An IEnumerable of the min positions to place the <paramref name="r"/> around the
        /// given <paramref name="spatial"/>.</returns>
        static IEnumerable<Vector2> GetPositionsAroundSpatial(ISpatial spatial, Rectangle r)
        {
            // Top
            yield return new Vector2(r.X, spatial.Position.Y - r.Height - 1);

            // Bottom
            yield return new Vector2(r.X, spatial.Max.Y + 1);

            // Left
            yield return new Vector2(spatial.Position.X - r.Width - 1, r.Y);

            // Right
            yield return new Vector2(spatial.Max.X + 1, r.Y);

            // Top, left-aligned
            yield return new Vector2(spatial.Position.X, spatial.Position.Y - r.Height - 1);

            // Top, right-aligned
            yield return new Vector2(spatial.Max.X - r.Width, spatial.Position.Y - r.Height - 1);

            // Bottom, left-aligned
            yield return new Vector2(spatial.Position.X, spatial.Max.Y + 1);

            // Bottom, right-aligned
            yield return new Vector2(spatial.Max.X - r.Width, spatial.Max.Y + 1);

            // Left, top-aligned
            yield return new Vector2(spatial.Position.X - r.Width - 1, spatial.Position.Y);

            // Left, bottom-aligned
            yield return new Vector2(spatial.Position.X - r.Width - 1, spatial.Max.Y - r.Height);

            // Right, top-aligned
            yield return new Vector2(spatial.Max.X + 1, spatial.Position.Y);

            // Right, bottom-aligned
            yield return new Vector2(spatial.Max.X + 1, spatial.Max.Y - r.Height);
        }

        /// <summary>
        /// Gets an IEnumerable of the <see cref="Type"/>s to build <see cref="ISpatialCollection"/>s for. This should include
        /// all the <see cref="Type"/>s that are used frequently when querying the map's spatial collection.
        /// </summary>
        /// <returns>An IEnumerable of the <see cref="Type"/>s to build <see cref="ISpatialCollection"/>s for.</returns>
        protected virtual IEnumerable<Type> GetSpatialTypes()
        {
            return new Type[]
            { typeof(Entity), typeof(DynamicEntity), typeof(CharacterEntity), typeof(WallEntityBase), typeof(ItemEntityBase) };
        }

        public bool IsInMapBoundaries(Vector2 min, Vector2 max)
        {
            return IsInMapBoundaries(min) && IsInMapBoundaries(max);
        }

        public bool IsInMapBoundaries(Entity entity)
        {
            return IsInMapBoundaries(entity.ToRectangle());
        }

        public bool IsInMapBoundaries(Rectangle rect)
        {
            var min = new Vector2(rect.X, rect.Y);
            var max = new Vector2(rect.Right, rect.Bottom);
            return IsInMapBoundaries(min, max);
        }

        public bool IsInMapBoundaries(Vector2 point)
        {
            return (point.X >= 0) && (point.Y >= 0) && (point.X < Width) && (point.Y < Height);
        }

        /// <summary>
        /// Checks if a given file is a valid map file by taking a quick look at the file, but not
        /// actually loading it. Files that are validated by this can still fail to load if they
        /// are corrupt.
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if the file is a valid map file, else false</returns>
        public static bool IsValidMapFile(string filePath)
        {
            // Check if the file exists
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            // Check the suffix
            if (!filePath.EndsWith("." + MapFileSuffix, StringComparison.OrdinalIgnoreCase))
                return false;

            // Check if the file is named properly
            MapIndex index;
            if (!Parser.Invariant.TryParse(Path.GetFileNameWithoutExtension(filePath), out index))
                return false;

            // Validate the file index
            if (index < 1)
                return false;

            // All checks passed
            return true;
        }

        /// <summary>
        /// Checks if a <see cref="Rectangle"/> is in a place that is inside of the map and does not intersect
        /// any <see cref="WallEntityBase"/>s.
        /// </summary>
        /// <param name="rect">The <see cref="Rectangle"/> containing the placement to check.</param>
        /// <returns>True if the <see cref="Rectangle"/> is in an area that does not intersect any
        /// <see cref="WallEntityBase"/>s; otherwise false.</returns>
        public bool IsValidPlacementPosition(Rectangle rect)
        {
            return IsInMapBoundaries(rect) && !Spatial.Contains<WallEntity>(rect);
        }

        public bool IsValidPlacementPosition(Vector2 position, Vector2 size)
        {
            var rect = new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
            return IsValidPlacementPosition(rect);
        }

        /// <summary>
        /// Checks if a <see cref="Rectangle"/> is in a place that is inside of the map and does not intersect
        /// any <see cref="WallEntityBase"/>s.
        /// </summary>
        /// <param name="r">The <see cref="Rectangle"/> that represents the area to check.</param>
        /// <param name="closestValidPosition">When this method returns false, contains the closest valid position
        /// that the <see cref="Rectangle"/> can be at to not intersect any <see cref="WallEntityBase"/>s.</param>
        /// <param name="validPositionFound">When this method returns false, contains if the
        /// <see cref="closestValidPosition"/> that was found is valid. If false, no near-by legal position could be
        /// found that the <see cref="Rectangle"/> could occupy without causing any intersections.</param>
        /// <returns>True if the <see cref="Rectangle"/> is in an area that does not intersect any
        /// <see cref="WallEntityBase"/>s; otherwise false.</returns>
        public bool IsValidPlacementPosition(Rectangle r, out Vector2 closestValidPosition, out bool validPositionFound)
        {
            // Perform the initial check to see if we need to even find a new position
            if (IsValidPlacementPosition(r))
            {
                // No intersections - already a valid position
                closestValidPosition = new Vector2(r.X, r.Y);
                validPositionFound = true;
                return true;
            }

            // Intersections were found, so we have to find a valid position
            // First, grab the walls in the region around the cb
            var nearbyWallsRect = new Rectangle(r.X - _findValidPlacementPadding, r.Y - _findValidPlacementPadding,
                                                r.Width + (_findValidPlacementPadding * 2),
                                                r.Height + (_findValidPlacementPadding * 2));
            var nearbyWalls = Spatial.GetMany<WallEntityBase>(nearbyWallsRect);

            // Next, find the legal positions we can place the cb
            var cbSize = new Vector2(r.Width, r.Height);
            var validPlacementPositions =
                nearbyWalls.SelectMany(wall => GetPositionsAroundSpatial(wall, r)).Where(p => IsValidPlacementPosition(p, cbSize));

            // If there are 0 legal positions, we're F'd in the A
            if (validPlacementPositions.Count() == 0)
            {
                // Failure :(
                validPositionFound = false;
                closestValidPosition = Vector2.Zero;
            }
            else
            {
                // One or more legal positions found, so find the closest one and use that
                validPositionFound = true;
                var cbMin = new Vector2(r.X, r.Y);
                closestValidPosition = validPlacementPositions.MinElement(x => x.QuickDistance(cbMin));
            }

            return false;
        }

        /// <summary>
        /// Confirms that an entity being moved by a given offset will
        /// keep the offset in the map boundaries, modifying the offset
        /// if needed to stay in the map.
        /// </summary>
        /// <param name="entity">Entity to be moved.</param>
        /// <param name="offset">Amount to add the entity's position.</param>
        public void KeepInMap(Entity entity, ref Vector2 offset)
        {
            if (entity == null)
            {
                const string errmsg = "Parameter `entity` is null.";
                Debug.Fail(errmsg);
                if (log.IsErrorEnabled)
                    log.Error(errmsg);
                return;
            }

            if (entity.Position.X + offset.X < 0)
                offset.X = -entity.Position.X;
            else if (entity.Max.X + offset.X > Width)
                offset.X = entity.Max.X - Width;

            if (entity.Position.Y + offset.Y < 0)
                offset.Y = -entity.Position.Y;
            else if (entity.Max.Y + offset.Y > Height)
                offset.Y = entity.Max.Y - Height;
        }

        /// <summary>
        /// Loads the map from the specified content path.
        /// </summary>
        /// <param name="contentPath">ContentPath to load the map from.</param>
        /// <param name="loadDynamicEntities">If true, the DynamicEntities will be loaded from the file. If false,
        /// all DynamicEntities will be skipped.</param>
        /// <param name="dynamicEntityFactory">The <see cref="IDynamicEntityFactory"/> used to load the
        /// <see cref="DynamicEntity"/>s.</param>
        public void Load(ContentPaths contentPath, bool loadDynamicEntities, IDynamicEntityFactory dynamicEntityFactory)
        {
            string path = contentPath.Maps.Join(Index + "." + MapFileSuffix);
            Load(path, loadDynamicEntities, dynamicEntityFactory);
        }

        /// <summary>
        /// Loads the map.
        /// </summary>
        /// <param name="filePath">Path to the file to load.</param>
        /// <param name="loadDynamicEntities">If true, the DynamicEntities will be loaded from the file. If false,
        /// all DynamicEntities will be skipped.</param>
        /// <param name="dynamicEntityFactory">The <see cref="IDynamicEntityFactory"/> used to load the
        /// <see cref="DynamicEntity"/>s.</param>
        protected virtual void Load(string filePath, bool loadDynamicEntities, IDynamicEntityFactory dynamicEntityFactory)
        {
            if (dynamicEntityFactory == null)
                throw new ArgumentNullException("dynamicEntityFactory");

            if (!File.Exists(filePath))
            {
                const string errmsg = "Map file `{0}` does not exist - unable to load map.";
                if (log.IsErrorEnabled)
                    log.ErrorFormat(errmsg, filePath);
                throw new ArgumentException("filePath");
            }

            var r = new XmlValueReader(filePath, _rootNodeName);
            LoadHeader(r);
            LoadWalls(r);
            LoadDynamicEntities(r, loadDynamicEntities, dynamicEntityFactory);
            LoadMisc(r.ReadNode(_miscNodeName));
        }

        void LoadDynamicEntities(IValueReader r, bool loadDynamicEntities, IDynamicEntityFactory dynamicEntityFactory)
        {
            var loadedDynamicEntities = r.ReadManyNodes<DynamicEntity>(_dynamicEntitiesNodeName, dynamicEntityFactory.Read);

            // Add the loaded DynamicEntities to the map
            if (loadDynamicEntities)
            {
                foreach (var dynamicEntity in loadedDynamicEntities)
                {
                    AddEntity(dynamicEntity);
                }
            }
        }

        /// <summary>
        /// Loads the header information for the map.
        /// </summary>
        /// <param name="r">XmlReader used to load the map file.</param>
        void LoadHeader(IValueReader r)
        {
            if (r == null)
                throw new ArgumentNullException("r");

            var nodeReader = r.ReadNode(_headerNodeName);

            // Read the values
            Name = nodeReader.ReadString(_headerNodeNameKey);
            Music = nodeReader.ReadString(_headerNodeMusicKey);
            _width = nodeReader.ReadFloat(_headerNodeWidthKey);
            _height = nodeReader.ReadFloat(_headerNodeHeightKey);
            Indoors = nodeReader.ReadBool(_headerNodeIndoorsKey);

            // Set the size for the spatial
            Spatial.SetAreaSize(Size);
        }

        /// <summary>
        /// Handles loading of custom values.
        /// </summary>
        /// <param name="r">IValueReader to read the misc values from.</param>
        protected virtual void LoadMisc(IValueReader r)
        {
        }

        /// <summary>
        /// Loads the wall information for the map.
        /// </summary>
        /// <param name="r">XmlReader used to load the map file.</param>
        void LoadWalls(IValueReader r)
        {
            if (r == null)
                throw new ArgumentNullException("r");

            var loadedWalls = r.ReadManyNodes<WallEntityBase>(_wallsNodeName, CreateWall);

            // Add the loaded walls to the map
            foreach (var wall in loadedWalls)
            {
                AddEntity(wall);
            }
        }

        /// <summary>
        /// Removes an entity from the map.
        /// </summary>
        /// <param name="entity">Entity to remove from the map.</param>
        public void RemoveEntity(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException("entity");

            // Remove the listeners
            entity.Disposed -= Entity_Disposed;

            // Remove the entity from the entity list
            if (!_entities.Remove(entity))
            {
                // Entity must have already been removed, since it wasn't in the entities list
                Debug.Fail("entity was not in the entities list");
            }

            var asUpdateable = entity as IUpdateableEntity;
            if (asUpdateable != null && !_updateableEntities.Remove(asUpdateable))
                Debug.Fail("Updateable entity was not in the updateable entities list");

            // If a DynamicEntity, remove it from the DynamicEntities list
            DynamicEntity dynamicEntity;
            if ((dynamicEntity = entity as DynamicEntity) != null)
            {
                Debug.Assert(_dynamicEntities[(int)dynamicEntity.MapEntityIndex] == dynamicEntity,
                             "DynamicEntity is holding an invalid MapEntityIndex!");
                _dynamicEntities.RemoveAt((int)dynamicEntity.MapEntityIndex);
            }

            // Remove the entity from the grid
            Spatial.Remove(entity);

            // Allow for additional processing
            EntityRemoved(entity);
        }

        /// <summary>
        /// Resizes the Entity to the supplied size, but forces the Entity to remain in the map's boundaries.
        /// </summary>
        /// <param name="entity">Entity to teleport</param>
        /// <param name="size">New size of the entity</param>
        public void SafeResizeEntity(Entity entity, Vector2 size)
        {
            if (entity == null)
            {
                Debug.Fail("entity is null.");
                return;
            }

            // Ensure the entity will be in the map
            var newMax = entity.Position + size;
            if (newMax.X > Width)
                size.X = Width - entity.Position.X;
            if (newMax.Y > Height)
                size.Y = Height - entity.Position.Y;

            // Set the size
            entity.Resize(size);
        }

        /// <summary>
        /// Performs an Entity.Teleport() call to the supplied position, but forces the Entity to remain
        /// in the map's boundaries.
        /// </summary>
        /// <param name="entity">Entity to teleport</param>
        /// <param name="pos">Position to teleport to</param>
        public void SafeTeleportEntity(Entity entity, Vector2 pos)
        {
            if (entity == null)
            {
                Debug.Fail("entity is null.");
                return;
            }

            // Ensure the entity will be in the map
            if (pos.X < 0)
                pos.X = 0;
            if (pos.Y < 0)
                pos.Y = 0;

            var max = pos + entity.Size;
            if (max.X > Width)
                pos.X = Width - entity.Size.X;
            if (max.Y > Height)
                pos.Y = Height - entity.Size.Y;

            // Teleport to the altered position
            entity.Teleport(pos);
        }

        /// <summary>
        /// Saves the map to a file to the specified content path.
        /// </summary>
        /// <param name="mapIndex">Map index to save as.</param>
        /// <param name="contentPath">Content path to save the map file to.</param>
        /// <param name="dynamicEntityFactory">The <see cref="IDynamicEntityFactory"/> used to load the
        /// <see cref="DynamicEntity"/>s.</param>
        public void Save(MapIndex mapIndex, ContentPaths contentPath, IDynamicEntityFactory dynamicEntityFactory)
        {
            var path = contentPath.Maps.Join(mapIndex + "." + MapFileSuffix);
            Save(path, dynamicEntityFactory);

            if (Saved != null)
                Saved(this);
        }

        /// <summary>
        /// Saves the map to a file to the specified file path.
        /// </summary>
        /// <param name="filePath">Path to save the map file at.</param>
        /// <param name="dynamicEntityFactory">The <see cref="IDynamicEntityFactory"/> used to load the
        /// <see cref="DynamicEntity"/>s.</param>
        void Save(string filePath, IDynamicEntityFactory dynamicEntityFactory)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("filePath");
            if (dynamicEntityFactory == null)
                throw new ArgumentNullException("dynamicEntityFactory");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using (IValueWriter w = new XmlValueWriter(filePath, _rootNodeName))
            {
                SaveHeader(w);
                SaveWalls(w);
                SaveDynamicEntities(w, dynamicEntityFactory);

                w.WriteStartNode(_miscNodeName);
                SaveMisc(w);
                w.WriteEndNode(_miscNodeName);
            }
        }

        void SaveDynamicEntities(IValueWriter w, IDynamicEntityFactory dynamicEntityFactory)
        {
            w.WriteManyNodes(_dynamicEntitiesNodeName, DynamicEntities, dynamicEntityFactory.Write);
        }

        /// <summary>
        /// Saves the map header
        /// </summary>
        /// <param name="w">IValueWriter to write to.</param>
        void SaveHeader(IValueWriter w)
        {
            if (w == null)
                throw new ArgumentNullException("w");

            w.WriteStartNode(_headerNodeName);
            {
                w.Write(_headerNodeNameKey, "INSERT VALUE");
                w.Write(_headerNodeMusicKey, Music);
                w.Write(_headerNodeWidthKey, Width);
                w.Write(_headerNodeHeightKey, Height);
                w.Write(_headerNodeIndoorsKey, Indoors);
            }
            w.WriteEndNode(_headerNodeName);
        }

        /// <summary>
        /// When overridden in the derived class, saves misc map information specific to the derived class.
        /// </summary>
        /// <param name="w">IValueWriter to write to.</param>
        protected virtual void SaveMisc(IValueWriter w)
        {
        }

        /// <summary>
        /// Saves the map walls
        /// </summary>
        /// <param name="w">IValueWriter to write to.</param>
        void SaveWalls(IValueWriter w)
        {
            if (w == null)
                throw new ArgumentNullException("w");

            var wallsToSave = Entities.OfType<WallEntityBase>();
            w.WriteManyNodes(_wallsNodeName, wallsToSave, ((writer, item) => item.Write(writer)));
        }

        /// <summary>
        /// Sets the new dimensions of the map and trims
        /// objects that exceed the new dimension
        /// </summary>
        /// <param name="newSize">New size of the map</param>
        public void SetDimensions(Vector2 newSize)
        {
            if (Size == newSize)
                return;

            // Remove any objects outside of the new dimensions
            if (Size.X > newSize.X || Size.Y > newSize.Y)
            {
                var outOfRangeEntities = Entities.Where(x => x.Max.X > newSize.X || x.Max.Y > newSize.Y).ToImmutable();
                foreach (var entity in outOfRangeEntities)
                {
                    entity.Dispose();
                    RemoveEntity(entity);
                }
            }

            // Update the map's size
            _width = newSize.X;
            _height = newSize.Y;

            // Update the spatial's size
            Spatial.SetAreaSize(Size);
        }

        /// <summary>
        /// Snaps a entity to any near-by entity
        /// </summary>
        /// <param name="entity">Entity to edit</param>
        /// <returns>New position for the entity</returns>
        public Vector2 SnapToWalls(Entity entity)
        {
            return SnapToWalls(entity, 20f);
        }

        /// <summary>
        /// Snaps a entity to any near-by entity
        /// </summary>
        /// <param name="entity">Entity to edit</param>
        /// <param name="maxDiff">Maximum position difference</param>
        /// <returns>New position for the entity</returns>
        public Vector2 SnapToWalls(Entity entity, float maxDiff)
        {
            var ret = new Vector2(entity.Position.X, entity.Position.Y);
            var pos = entity.Position - new Vector2(maxDiff / 2f);
            var size = entity.Size + new Vector2(maxDiff);
            var newRect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);

            foreach (var w in Spatial.GetMany<WallEntityBase>(newRect, x => x != entity))
            {
                // Selected wall right side to target wall left side
                if (Math.Abs(newRect.Right - w.Position.X) < maxDiff)
                    ret.X = w.Position.X - entity.Size.X;

                // Selected wall left side to target wall right side
                if (Math.Abs(w.Max.X - newRect.X) < maxDiff)
                    ret.X = w.Max.X;

                // Selected wall bottom to target wall top
                if (Math.Abs(newRect.Bottom - w.Position.Y) < maxDiff)
                    ret.Y = w.Position.Y - entity.Size.Y;

                // Selected wall top to target wall bottom
                if (Math.Abs(w.Max.Y - newRect.Y) < maxDiff)
                    ret.Y = w.Max.Y;
            }

            return ret;
        }

        /// <summary>
        /// Tries to get the DynamicEntity at the specified index.
        /// </summary>
        /// <param name="index">Unique index of the DynamicEntity to get.</param>
        /// <param name="dynamicEntity">DynamicEntity found at the specified index, or null if none found.</param>
        /// <returns>True if the DynamicEntity was successfully found, else false.</returns>
        public bool TryGetDynamicEntity(MapEntityIndex index, out DynamicEntity dynamicEntity)
        {
            if (!_dynamicEntities.CanGet((int)index))
                dynamicEntity = null;
            else
                dynamicEntity = _dynamicEntities[(int)index];

            return (dynamicEntity != null);
        }

        /// <summary>
        /// Tries to get the DynamicEntity at the specified index.
        /// </summary>
        /// <typeparam name="T">Type of DynamicEntity to find.</typeparam>
        /// <param name="index">Unique index of the DynamicEntity to get.</param>
        /// <param name="dynamicEntity">DynamicEntity found at the specified index and of
        /// the specified type, otherwise null.</param>
        /// <returns>True if the DynamicEntity was successfully found, else false.</returns>
        public bool TryGetDynamicEntity<T>(MapEntityIndex index, out T dynamicEntity) where T : DynamicEntity
        {
            if (!_dynamicEntities.CanGet((int)index))
                dynamicEntity = null;
            else
                dynamicEntity = _dynamicEntities[(int)index] as T;

            return (dynamicEntity != null);
        }

        /// <summary>
        /// Tries to get the Map's index from the file path.
        /// </summary>
        /// <param name="path">File path to the map.</param>
        /// <param name="mapIndex">If this method returns true, contains the index of the map.</param>
        /// <returns>True if the parsing was successful; otherwise false.</returns>
        public static bool TryGetIndexFromPath(string path, out MapIndex mapIndex)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            var fileName = Path.GetFileNameWithoutExtension(path);
            return Parser.Invariant.TryParse(fileName, out mapIndex);
        }

        /// <summary>
        /// Updates the map.
        /// </summary>
        /// <param name="deltaTime">The amount of time that elapsed since the last update.</param>
        public virtual void Update(int deltaTime)
        {
            // Update the Entities
            // We use a for loop because entities might be added/removed when they update
            IUpdateableEntity current;
            int i = 0;
            while (true)
            {
                // Check if we hit the end
                if (i >= _updateableEntities.Count)
                    break;

                // Get and update the current entity
                current = _updateableEntities[i];

                if (current != null)
                    current.Update(this, deltaTime);

                // Only increment if the current has not changed
                // This way we can be sure to update everyone even if the entities collection changed
                if (i < _updateableEntities.Count && current == _updateableEntities[i])
                    i++;
            }
        }

        #region IMap Members

        /// <summary>
        /// Gets an IEnumerable of all the Entities on the Map.
        /// </summary>
        public IEnumerable<Entity> Entities
        {
            get { return _entities; }
        }

        /// <summary>
        /// Gets the height of the map in pixels
        /// </summary>
        public float Height
        {
            get { return _height; }
        }

        /// <summary>
        /// Gets the size of the map in pixels.
        /// </summary>
        public Vector2 Size
        {
            get { return new Vector2(Width, Height); }
        }

        /// <summary>
        /// Gets the <see cref="ISpatialCollection"/> for all the spatial objects on the map.
        /// </summary>
        public ISpatialCollection Spatial
        {
            get { return _spatialCollection; }
        }

        /// <summary>
        /// Gets the width of the map in pixels.
        /// </summary>
        public float Width
        {
            get { return _width; }
        }

        /// <summary>
        /// Checks if an Entity collides with any other entities. For each collision, <paramref name="entity"/>
        /// will call CollideInto(), and the Entity that <paramref name="entity"/> collided into will call
        /// CollideFrom().
        /// </summary>
        /// <param name="entity">Entity to check against other Entities. If the CollisionType is
        /// CollisionType.None, or if <paramref name="entity"/> is null, this will always return 0 and no
        /// collision detection will take place.</param>
        public void CheckCollisions(Entity entity)
        {
            // Check for a null entity
            if (entity == null)
            {
                const string errmsg = "Parameter `entity` is null.";
                Debug.Fail(errmsg);
                if (log.IsErrorEnabled)
                    log.Error(errmsg);
                return;
            }

            // Ensure the entity is in the map still
            var move = Vector2.Zero;
            var entityMin = entity.Position;
            var entityMax = entity.Max;

            if (entityMin.X < 0)
                move.X = -entityMin.X;
            else if (entityMax.X >= Size.X)
                move.X = Size.X - entityMax.X;

            if (entityMin.Y < 0)
                move.Y = -entityMin.Y;
            else if (entityMax.Y >= Size.Y)
                move.Y = Size.Y - entityMax.Y;

            entity.Move(move);

            // Perform collision detetion
            CheckCollisionsAgainstWalls(entity);
            CheckCollisionAgainstEntities(entity);
        }

        /// <summary>
        /// Finds the <see cref="WallEntityBase"/> that the <paramref name="stander"/> is standing on.
        /// </summary>
        /// <param name="stander">The <see cref="ISpatial"/> to check for standing on a <see cref="WallEntityBase"/>.</param>
        /// <returns>The best-fit <see cref="WallEntityBase"/> that the <paramref name="stander"/> is standing on, or
        /// null if they are not standing on any walls.</returns>
        public WallEntityBase FindStandingOn(ISpatial stander)
        {
            var rect = stander.GetStandingAreaRect();
            return Spatial.Get<WallEntityBase>(rect);
        }

        /// <summary>
        /// Gets the current time in milliseconds.
        /// </summary>
        /// <returns>The current time in milliseconds.</returns>
        public int GetTime()
        {
            return _getTime.GetTime();
        }

        #endregion

        #region IMapTable Members

        /// <summary>
        /// Gets the value of the database column `id`.
        /// </summary>
        MapIndex IMapTable.ID
        {
            get { return Index; }
        }

        /// <summary>
        /// Gets or sets the name of the map.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        public string Name
        {
            get { return _name; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                _name = value;
            }
        }

        /// <summary>
        /// Creates a deep copy of this table. All the values will be the same
        /// but they will be contained in a different object instance.
        /// </summary>
        /// <returns>
        /// A deep copy of this table.
        /// </returns>
        IMapTable IMapTable.DeepCopy()
        {
            return new MapTable(this);
        }

        #endregion
    }
}
﻿using System.Linq;

/*
namespace NetGore
{
    /// <summary>
    /// An implementation of <see cref="ISpatialCollection"/> that uses a grid internally to reduce the test
    /// set for all the queries.
    /// </summary>
    public abstract class GridSpatialBase : ISpatialCollection
    {
        /// <summary>
        /// Size of each segment of the wall grid in pixels (smallest requires more
        /// memory but often less checks (to an extent))
        /// </summary>
        const int _entityGridSize = 128;

        /// <summary>
        /// Two-dimensional grid of references to entities in that sector.
        /// </summary>
        List<ISpatial>[,] _entityGrid;

        Vector2 _mapSize;

        protected IEnumerable<ISpatial> this[int x, int y]
        {
            get
            {
                if (!IsLegalGridIndex(x, y))
                    return Enumerable.Empty<ISpatial>();

                return _entityGrid[x, y];
            }
        }

        protected IEnumerable<ISpatial> this[Point gridIndex]
        {
            get { return this[gridIndex.X, gridIndex.Y]; }
        }

        protected Point GridSize
        {
            get { return new Point(_entityGrid.GetLength(0), _entityGrid.GetLength(1)); }
        }

        void AddToGrid(ISpatial entity)
        {
            var minX = (int)entity.CB.Min.X / _entityGridSize;
            var minY = (int)entity.CB.Min.Y / _entityGridSize;
            var maxX = (int)entity.CB.Max.X / _entityGridSize;
            var maxY = (int)entity.CB.Max.Y / _entityGridSize;

            // Keep in range of the grid
            if (minX < 0)
                minX = 0;

            if (maxX >= GridSize.X)
                maxX = GridSize.X - 1;

            if (minY < 0)
                minY = 0;

            if (maxY >= GridSize.Y)
                maxY = GridSize.Y - 1;

            // Add to all the segments of the grid
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_entityGrid[x, y].Contains(entity))
                        _entityGrid[x, y].Add(entity);
                }
            }
        }

        /// <summary>
        /// Builds a two-dimensional array of Lists to use as the grid of entities.
        /// </summary>
        /// <returns>A two-dimensional array of Lists to use as the grid of entities.</returns>
        static List<ISpatial>[,] BuildISpatialGrid(float width, float height)
        {
            var gridWidth = (int)Math.Ceiling(width / _entityGridSize);
            var gridHeight = (int)Math.Ceiling(height / _entityGridSize);

            // Create the array
            var retGrid = new List<ISpatial>[gridWidth,gridHeight];

            // Create the lists
            for (var x = 0; x < gridWidth; x++)
            {
                for (var y = 0; y < gridHeight; y++)
                {
                    retGrid[x, y] = new List<ISpatial>();
                }
            }

            return retGrid;
        }

        static bool EmptyPred<T>(T e)
        {
            return true;
        }

        void ISpatial_OnMove(ISpatial entity, Vector2 oldPos)
        {
            UpdateISpatial(entity, oldPos);
            ForceISpatialInMapBoundaries(entity);
        }

        /// <summary>
        /// Checks if an ISpatial is in the map's boundaries and, if it is not, moves the ISpatial into the map's boundaries.
        /// </summary>
        /// <param name="entity">ISpatial to check.</param>
        void ForceISpatialInMapBoundaries(ISpatial entity)
        {
            var min = entity.CB.Min;
            var max = entity.CB.Max;

            if (min.X < 0)
                min.X = 0;
            if (min.Y < 0)
                min.Y = 0;
            if (max.X >= _mapSize.X)
                min.X = _mapSize.X - entity.CB.Width;
            if (max.Y >= _mapSize.Y)
                min.Y = _mapSize.Y - entity.CB.Height;

            if (min != entity.CB.Min)
                entity.Teleport(min);
        }

        IEnumerable<ISpatial> GetAllEntities()
        {
            if (_entityGrid == null)
                return Enumerable.Empty<ISpatial>();

            List<ISpatial> ret = new List<ISpatial>();
            foreach (var segment in _entityGrid)
            {
                ret.AddRange(segment);
            }

            return ret.Distinct();
        }

        /// <summary>
        /// Gets the grid segment for each intersection on the entity grid.
        /// </summary>
        /// <param name="rect">Map area to get the grid segments for.</param>
        /// <returns>An IEnumerable of all grid segments intersected by the specified <paramref name="rect"/>.</returns>
        protected IEnumerable<IEnumerable<ISpatial>> GetISpatialGrids(Rectangle rect)
        {
            var minX = rect.X / _entityGridSize;
            var minY = rect.Y / _entityGridSize;
            var maxX = rect.Right / _entityGridSize;
            var maxY = rect.Bottom / _entityGridSize;

            // Keep in range of the grid
            if (minX < 0)
                minX = 0;

            if (maxX >= GridSize.X)
                maxX = GridSize.X - 1;

            if (minY < 0)
                minY = 0;

            // NOTE: For some reason this last check here likes to fail a lot. I think it is because gravity pushes an ISpatial out of the map temporarily when they are down low. Ideally, this condition is NEVER reached.
            if (maxY >= GridSize.Y)
                maxY = GridSize.Y - 1;

            // Return the grid segments
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    yield return this[x, y];
                }
            }
        }

        /// <summary>
        /// Gets if the given point is a legal grid index.
        /// </summary>
        /// <param name="point">The grid x and y co-ordinate.</param>
        /// <returns>True if the given point is a legal grid index; otherwise false.</returns>
        protected bool IsLegalGridIndex(Point point)
        {
            return IsLegalGridIndex(point.X, point.Y);
        }

        /// <summary>
        /// Gets if the given point is a legal grid index.
        /// </summary>
        /// <param name="x">The grid x co-ordinate.</param>
        /// <param name="y">The grid y co-ordinate.</param>
        /// <returns>True if the given point is a legal grid index; otherwise false.</returns>
        protected bool IsLegalGridIndex(int x, int y)
        {
            return x >= 0 && x < _entityGrid.GetLength(0) && y >= 0 && y < _entityGrid.GetLength(1);
        }

        static Point MapPositionToGridIndex(Vector2 position)
        {
            var x = position.X / _entityGridSize;
            var y = position.Y / _entityGridSize;
            return new Point((int)x, (int)y);
        }

        /// <summary>
        /// Gets the Entities found intersecting the given region.
        /// </summary>
        /// <param name="rect">Region to check for Entities.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>All Entities found intersecting the given region.</returns>
        IEnumerable<ISpatial> MutableGetEntities(Rectangle rect, Predicate<ISpatial> condition)
        {
            var gridSegments = GetISpatialGrids(rect);
            var matches = gridSegments.SelectMany(x => x).Where(x => x.CB.Intersect(rect) && condition(x)).Distinct();
            return matches;
        }

        /// <summary>
        /// Gets all entities at the given point.
        /// </summary>
        /// <param name="p">The point to find the entities at.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to look for.</typeparam>
        /// <returns>All entities containing the given point that are of the given type.</returns>
        IEnumerable<T> MutableGetEntities<T>(Vector2 p, Predicate<T> condition) 
        {
            var gridSegment = this[MapPositionToGridIndex(p)];
            var matches = gridSegment.OfType<T>().Where(x => x.CB.HitTest(p) && condition(x));
            Debug.Assert(!matches.HasDuplicates(), "Somehow we had duplicates even though we only used one grid segment!");
            return matches;
        }

        /// <summary>
        /// Gets the Entities found intersecting the given region.
        /// </summary>
        /// <param name="rect">Region to check for Entities.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <typeparam name="T">Type of ISpatial to look for.</typeparam>
        /// <returns>All Entities found intersecting the given region.</returns>
        IEnumerable<T> MutableGetEntities<T>(Rectangle rect, Predicate<T> condition) 
        {
            var gridSegments = GetISpatialGrids(rect);
            var matches = gridSegments.SelectMany(x => x).OfType<T>().Where(x => x.CB.Intersect(rect) && condition(x)).Distinct();
            return matches;
        }

        /// <summary>
        /// Gets all entities containing a given point.
        /// </summary>
        /// <param name="p">Point to find the entities at.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>All of the entities at the given point.</returns>
        IEnumerable<ISpatial> MutableGetEntities(Vector2 p, Predicate<ISpatial> condition)
        {
            var gridSegment = this[MapPositionToGridIndex(p)];
            var matches = gridSegment.Where(x => x.CB.HitTest(p) && condition(x));
            Debug.Assert(!matches.HasDuplicates(), "Somehow we had duplicates even though we only used one grid segment!");
            return matches;
        }

        /// <summary>
        /// Sets the source map to a new size and clears out all existing entities in the grid.
        /// </summary>
        /// <param name="size">The new size of the source map.</param>
        public void SetMapSize(Vector2 size)
        {
            var entities = GetAllEntities();

            _mapSize = size;
            _entityGrid = BuildISpatialGrid(size.X, size.Y);

            // Re-add the items to the grid
            foreach (var entity in entities)
            {
                AddToGrid(entity);
            }
        }

        /// <summary>
        /// When overridden in the derived class, returns an immutable version of the <see cref="collection"/>. This is
        /// abstract so the code can be reused in a derived class that does not need to make the collection immutable
        /// because it does not contains entities that move.
        /// </summary>
        /// <typeparam name="T">The Type of element.</typeparam>
        /// <param name="collection">The collection to make immutable.</param>
        /// <returns>The <paramref name="collection"/> as immutable.</returns>
        protected abstract IEnumerable<T> ToImmutable<T>(IEnumerable<T> collection);

        public void UpdateISpatial(ISpatial entity, Vector2 oldPos)
        {
            // NOTE: !! Does this really need to be public?

            // FUTURE: Can optimize this method quite a lot by only adding/removing from changed grid segments

            // Check that the entity changed grid segments by comparing the lowest grid segments
            // of the old position and current position
            var minX = (int)oldPos.X / _entityGridSize;
            var minY = (int)oldPos.Y / _entityGridSize;
            var newMinX = (int)entity.CB.Min.X / _entityGridSize;
            var newMinY = (int)entity.CB.Min.Y / _entityGridSize;

            if (minX == newMinX && minY == newMinY)
                return; // No change in grid segment

            var maxX = (int)(oldPos.X + entity.CB.Width) / _entityGridSize;
            var maxY = (int)(oldPos.Y + entity.CB.Height) / _entityGridSize;

            // Keep in range of the grid
            if (minX < 0)
                minX = 0;

            if (maxX >= GridSize.X)
                maxX = GridSize.X - 1;

            if (minY < 0)
                minY = 0;

            if (maxY >= GridSize.Y)
                maxY = GridSize.Y - 1;

            // Remove the entity from the old grid position
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    _entityGrid[x, y].Remove(entity);
                }
            }

            // Re-add the entity to the grid
            AddToGrid(entity);
        }

        #region ISpatialCollection Members

        /// <summary>
        /// Adds multiple entities to the spatial collection.
        /// </summary>
        /// <param name="entities">The entities to add.</param>
        public void Add(IEnumerable<ISpatial> entities)
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        /// <summary>
        /// Adds a single <see cref="ISpatial"/> to the spatial collection.
        /// </summary>
        /// <param name="entity">The <see cref="ISpatial"/> to add.</param>
        public void Add(ISpatial entity)
        {
            AddToGrid(entity);
            entity.OnMove += ISpatial_OnMove;
        }

        /// <summary>
        /// Checks if this spatial collection contains the given <paramref name="entity"/>.
        /// </summary>
        /// <param name="entity">The <see cref="ISpatial"/> to look for.</param>
        /// <returns>True if this spatial collection contains the given <paramref name="entity"/>; otherwise false.</returns>
        public bool Contains(ISpatial entity)
        {
            // The first place to check would be the place the entity should be
            if (ContainsEntities(entity.CB.ToRectangle(), x => x == entity))
                return true;

            // If the entity isn't where it should be, check every segment
            foreach (var gridSegment in _entityGrid)
            {
                if (gridSegment.Contains(entity))
                {
                    Debug.Fail("The entity was found in the spatial, but not where it was expected to be...");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes an <see cref="ISpatial"/> from the spatial collection.
        /// </summary>
        /// <param name="entity">The <see cref="ISpatial"/> to remove.</param>
        public void Remove(ISpatial entity)
        {
            entity.OnMove -= ISpatial_OnMove;

            foreach (var gridSegment in _entityGrid)
            {
                gridSegment.Remove(entity);
            }
        }

        /// <summary>
        /// Gets the first <see cref="ISpatial"/> found in the given region.
        /// </summary>
        /// <param name="rect">Region to find the <see cref="ISpatial"/> in.</param>
        /// <param name="condition">Additional condition an <see cref="ISpatial"/> must meet.</param>
        /// <param name="condition">Condition the Entities must meet.</param>
        /// <returns>The first <see cref="ISpatial"/> found in the given region, or null if none found.</returns>
        public T GetEntity<T>(Rectangle rect, Predicate<T> condition) 
        {
            return MutableGetEntities(rect, condition).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first <see cref="ISpatial"/> found in the given region.
        /// </summary>
        /// <param name="rect">Region to find the <see cref="ISpatial"/> in.</param>
        /// <param name="condition">Additional condition an <see cref="ISpatial"/> must meet.</param>
        /// <returns>The first <see cref="ISpatial"/> found in the given region, or null if none found.</returns>
        public ISpatial GetEntity(Rectangle rect, Predicate<ISpatial> condition)
        {
            return MutableGetEntities(rect, condition).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first <see cref="ISpatial"/> found at the given point.
        /// </summary>
        /// <param name="p">Point to find the entity at.</param>
        /// <param name="condition">Condition the <see cref="ISpatial"/> must meet.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to look for. Any other type of <see cref="ISpatial"/>
        /// will be ignored.</typeparam>
        /// <returns>First <see cref="ISpatial"/> found at the given point, or null if none found.</returns>
        public T GetEntity<T>(Vector2 p, Predicate<T> condition) 
        {
            return MutableGetEntities(p, condition).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first <see cref="ISpatial"/> found at the given point.
        /// </summary>
        /// <param name="p">Point to find the entity at.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to look for. Any other type of <see cref="ISpatial"/>
        /// will be ignored.</typeparam>
        /// <returns>First <see cref="ISpatial"/> found at the given point, or null if none found.</returns>
        public T GetEntity<T>(Vector2 p) 
        {
            return MutableGetEntities<T>(p, EmptyPred).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first <see cref="ISpatial"/> found at the given point.
        /// </summary>
        /// <param name="p">Point to find the entity at.</param>
        /// <returns>First <see cref="ISpatial"/> found at the given point, or null if none found.</returns>
        public ISpatial GetEntity(Vector2 p)
        {
            return MutableGetEntities(p, EmptyPred).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first <see cref="ISpatial"/> found at the given point.
        /// </summary>
        /// <param name="p">Point to find the entity at.</param>
        /// <param name="condition">Condition the <see cref="ISpatial"/> must meet.</param>
        /// <returns>First <see cref="ISpatial"/> found at the given point, or null if none found.</returns>
        public ISpatial GetEntity(Vector2 p, Predicate<ISpatial> condition)
        {
            return MutableGetEntities(p, condition).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first ISpatial found in the given region
        /// </summary>
        /// <param name="rect">Region to check for the ISpatial</param>
        /// <returns>First ISpatial found at the given point, or null if none found</returns>
        public ISpatial GetEntity(Rectangle rect)
        {
            return MutableGetEntities(rect, EmptyPred).FirstOrDefault();
        }

        /// <summary>
        /// Gets the first ISpatial found in the given region
        /// </summary>
        /// <param name="rect">Region to check for the ISpatial</param>
        /// <typeparam name="T">Type to convert to</typeparam>
        /// <returns>First ISpatial found at the given point, or null if none found</returns>
        public T GetEntity<T>(Rectangle rect) 
        {
            return MutableGetEntities<T>(rect, EmptyPred).FirstOrDefault();
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="rect">The map area to check.</param>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities(Rectangle rect)
        {
            return ContainsEntities(rect, EmptyPred);
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="rect">The map area to check.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities(Rectangle rect, Predicate<ISpatial> condition)
        {
            return GetISpatialGrids(rect).SelectMany(x => x).Any(x => x.CB.Intersect(rect) && condition(x));
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="rect">The map area to check.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to check against. All other types of
        /// <see cref="ISpatial"/> will be ignored.</typeparam>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities<T>(Rectangle rect) 
        {
            return ContainsEntities<T>(rect, EmptyPred);
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="rect">The map area to check.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to check against. All other types of
        /// <see cref="ISpatial"/> will be ignored.</typeparam>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities<T>(Rectangle rect, Predicate<T> condition) 
        {
            return GetISpatialGrids(rect).SelectMany(x => x).OfType<T>().Any(x => x.CB.Intersect(rect) && condition(x));
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="point">The map point to check.</param>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities(Vector2 point)
        {
            return ContainsEntities(point, EmptyPred);
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="point">The map point to check.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities(Vector2 point, Predicate<ISpatial> condition)
        {
            var gridSegment = this[MapPositionToGridIndex(point)];
            return gridSegment.Any(x => x.CB.HitTest(point) && condition(x));
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="point">The map point to check.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to check against. All other types of
        /// <see cref="ISpatial"/> will be ignored.</typeparam>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities<T>(Vector2 point) 
        {
            return ContainsEntities<T>(point, EmptyPred);
        }

        /// <summary>
        /// Gets if the specified area or location contains any entities.
        /// </summary>
        /// <param name="point">The map point to check.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to check against. All other types of
        /// <see cref="ISpatial"/> will be ignored.</typeparam>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>True if the specified area or location contains any entities; otherwise false.</returns>
        public bool ContainsEntities<T>(Vector2 point, Predicate<T> condition) 
        {
            var gridSegment = this[MapPositionToGridIndex(point)];
            return gridSegment.OfType<T>().Any(x => x.CB.HitTest(point) && condition(x));
        }

        /// <summary>
        /// Gets the Entities found intersecting the given region.
        /// </summary>
        /// <param name="rect">Region to check for Entities.</param>
        /// <returns>All Entities found intersecting the given region.</returns>
        public IEnumerable<ISpatial> GetEntities(Rectangle rect)
        {
            return GetEntities(rect, EmptyPred);
        }

        /// <summary>
        /// Gets the Entities found intersecting the given region.
        /// </summary>
        /// <param name="rect">Region to check for Entities.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>All Entities found intersecting the given region.</returns>
        public IEnumerable<ISpatial> GetEntities(Rectangle rect, Predicate<ISpatial> condition)
        {
            return MutableGetEntities(rect, condition).ToImmutable();
        }

        /// <summary>
        /// Gets all entities at the given point.
        /// </summary>
        /// <param name="p">The point to find the entities at.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to look for.</typeparam>
        /// <returns>All entities containing the given point that are of the given type.</returns>
        public IEnumerable<T> GetEntities<T>(Vector2 p) 
        {
            return GetEntities<T>(p, EmptyPred);
        }

        /// <summary>
        /// Gets all entities at the given point.
        /// </summary>
        /// <param name="p">The point to find the entities at.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <typeparam name="T">The type of <see cref="ISpatial"/> to look for.</typeparam>
        /// <returns>All entities containing the given point that are of the given type.</returns>
        public IEnumerable<T> GetEntities<T>(Vector2 p, Predicate<T> condition) 
        {
            return MutableGetEntities(p, condition).ToImmutable();
        }

        /// <summary>
        /// Gets the Entities found intersecting the given region.
        /// </summary>
        /// <param name="rect">Region to check for Entities.</param>
        /// <typeparam name="T">Type of ISpatial to look for.</typeparam>
        /// <returns>All Entities found intersecting the given region.</returns>
        public IEnumerable<T> GetEntities<T>(Rectangle rect) 
        {
            return GetEntities<T>(rect, EmptyPred);
        }

        /// <summary>
        /// Gets the Entities found intersecting the given region.
        /// </summary>
        /// <param name="rect">Region to check for Entities.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <typeparam name="T">Type of ISpatial to look for.</typeparam>
        /// <returns>All Entities found intersecting the given region.</returns>
        public IEnumerable<T> GetEntities<T>(Rectangle rect, Predicate<T> condition) 
        {
            return MutableGetEntities(rect, condition).ToImmutable();
        }

        /// <summary>
        /// Gets all entities containing a given point.
        /// </summary>
        /// <param name="p">Point to find the entities at.</param>
        /// <returns>All of the entities at the given point.</returns>
        public IEnumerable<ISpatial> GetEntities(Vector2 p)
        {
            return GetEntities(p, EmptyPred);
        }

        /// <summary>
        /// Gets all entities containing a given point.
        /// </summary>
        /// <param name="p">Point to find the entities at.</param>
        /// <param name="condition">The additional condition an <see cref="ISpatial"/> must match to be included.</param>
        /// <returns>All of the entities at the given point.</returns>
        public IEnumerable<ISpatial> GetEntities(Vector2 p, Predicate<ISpatial> condition)
        {
            return MutableGetEntities(p, condition).ToImmutable();
        }

        #endregion
    }
}
 */
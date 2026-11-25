using System;
namespace RogueCards.Domain
{
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public const int GridSize = 5;
        public static readonly GridPos[] OrthogonalDirections =
    {
        new GridPos(0, -1),
        new GridPos(1, 0),
        new GridPos(0, 1),
        new GridPos(-1, 0)
    };

        public static readonly GridPos[] DiagonalDirections =
        {
        new GridPos(1, -1),
        new GridPos(1, 1),
        new GridPos(-1, 1),
        new GridPos(-1, -1)
    };

        public static readonly GridPos[] AllEightDirections =
        {
        new GridPos(0, -1),
        new GridPos(1, -1),
        new GridPos(1, 0),
        new GridPos(1, 1),
        new GridPos(0, 1),
        new GridPos(-1, 1),
        new GridPos(-1, 0),
        new GridPos(-1, -1)
    };

        public int X { get; }

        public int Y { get; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool IsValid()
        {
            return X >= 0 && X < GridSize && Y >= 0 && Y < GridSize;
        }

        public GridPos Offset(int dx, int dy)
        {
            return new GridPos(X + dx, Y + dy);
        }

        public GridPos Offset(GridPos delta)
        {
            return new GridPos(X + delta.X, Y + delta.Y);
        }

        public int ManhattanDistance(GridPos other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public int ChebyshevDistance(GridPos other)
        {
            return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
        }

        public bool IsEdgeTile()
        {
            if (!IsValid())
            {
                return false;
            }

            return X == 0 || X == GridSize - 1 || Y == 0 || Y == GridSize - 1;
        }

        public bool IsOrthogonalDirection()
        {
            bool horizontal = Math.Abs(X) == 1 && Y == 0;
            bool vertical = X == 0 && Math.Abs(Y) == 1;
            return horizontal || vertical;
        }

        public bool IsDiagonalDirection()
        {
            return Math.Abs(X) == 1 && Math.Abs(Y) == 1;
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            if (obj is GridPos other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(GridPos left, GridPos right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPos left, GridPos right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }
}
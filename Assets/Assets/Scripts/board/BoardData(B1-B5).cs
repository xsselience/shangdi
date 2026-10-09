// B部分基础数据：逻辑坐标、棋盘格和棋子。
// 原有多个脚本已合并；公开类型名称保持不变。
using System;
using UnityEngine;
using System.Collections.Generic;


// ===== 原文件：Boardtype.cs =====
using System;
namespace Game.Board
{ //棋子元素属性
  public enum ElementType
    {
        None = 0,
        Fire = 1, // 火
        Water = 2,//水
        Wind= 3,//风
        ground = 4,//土
    }
    //棋子的类型（普通和特种)
    public enum PieceKind
    {
        Normal= 0,
        Special = 1
        
    }
    //棋子的逻辑坐标
   public struct GridCoord : IEquatable<GridCoord>
    { 
        public int x;
        public int y;


        //获取横纵坐标
        public int X {  get { return x; } }
        public int Y { get { return y; } }


       //建立新坐标
    public GridCoord(int x, int y)
        { this.x = x; this.y = y; }


        //根据偏移量计算一个新的坐标
        public GridCoord Offset(int deltaX, int deltaY)
        {
            return new GridCoord(
                x + deltaX,
                y + deltaY
            );
        }

        //判断当前坐标和另一个坐标是否相同
        public bool Equals(GridCoord other)
        {
            return x == other.x && y == other.y;
        }
        public override bool Equals(object obj)
        {
            // 如果传进来的对象不是 GridCoord，
            // 那么它不可能和当前坐标相同。
            if (!(obj is GridCoord))
            {
                return false;
            }

            // 把 object 转换为 GridCoord，
            // 然后调用上面专门比较坐标的方法。
            return Equals((GridCoord)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // 用于哈希计算
                
                return (x * 397) ^ y;
            }
        }
        //重载 == 运算符
        public static bool operator ==(
            GridCoord left,
            GridCoord right)
        {
            return left.Equals(right);
        }

        // 重载 != 运算符。
        public static bool operator !=(
            GridCoord left,
            GridCoord right)
        {
            return !left.Equals(right);
        }

        //把坐标转换成方便阅读的文字
        public override string ToString()
        {
            return "(" + x + ", " + y + ")";
        }

    }



}



// ===== 原文件：cellstate.cs =====
namespace Game.Board
{
    // 一个棋盘格的数据
  

    public class CellState
    {
        // 这个格子的逻辑坐标
        private readonly GridCoord coord;

        // 这个格子是否允许玩家操作
        private readonly bool canOperate;

        // 这个格子是否具有刷新权限
        private readonly bool canSpawn;

        // 这个格子是否存在棋盘障碍
        private readonly bool hasObstacle;

        // 当前放在这个格子里的棋子
        // null 表示没有棋子
        private PieceState piece;

        public GridCoord Coord
        {
            get { return coord; }
        }

        public bool CanOperate
        {
            get { return canOperate; }
        }

        public bool CanSpawn
        {
            get { return canSpawn; }
        }

        public bool HasObstacle
        {
            get { return hasObstacle; }
        }

        public PieceState Piece
        {
            get { return piece; }
        }

        /// <summary>
        /// 当前格子是否有棋子
        /// </summary>
        public bool HasPiece
        {
            get { return piece != null; }
        }

        /// <summary>
        /// 创建一个格子。
        /// 初始时格子里没有棋子
        /// </summary>
        public CellState(
            GridCoord coord,
            bool canOperate,
            bool canSpawn,
            bool hasObstacle)
        {
            this.coord = coord;
            this.canOperate = canOperate;
            this.canSpawn = canSpawn;
            this.hasObstacle = hasObstacle;

            piece = null;
        }

        /// <summary>
        /// 将棋子放入格子
        /// 这是内部数据操作，不是完整的玩家移动。
        /// </summary>
        internal void PlacePiece(PieceState newPiece)
        {
            if (newPiece == null)
            {
                throw new ArgumentNullException(
                    nameof(newPiece),
                    "放入格子的棋子不能是 null。"
                );
            }

            if (hasObstacle)
            {
                throw new InvalidOperationException(
                    "存在棋盘障碍的格子不能放入棋子。"
                );
            }

            if (piece != null)
            {
                throw new InvalidOperationException(
                    "这个格子已经有棋子，不能直接覆盖。"
                );
            }

            piece = newPiece;
        }

        /// <summary>
        /// 取出当前格子的棋子，并将格子清空
        /// 如果原来没有棋子，就返回 null
        /// </summary>
        internal PieceState RemovePiece()
        {
            PieceState removedPiece = piece;

            piece = null;

            return removedPiece;
        }
    }
}

// ===== 原文件：PieceState.cs =====
namespace Game.Board
{
    
    // 一颗棋子的数据
   
    
    public class PieceState
    {
        // 棋子的唯一编号
        private readonly int id;

        // 棋子的元素属性。
        private readonly ElementType element;

        // 棋子的普通类型
        private readonly PieceKind kind;

        
        // 获取棋子编号。
      
        public int Id
        {
            get { return id; }
        }

        /// <summary>
        /// 获取棋子属性
        /// </summary>
        public ElementType Element
        {
            get { return element; }
        }

        /// <summary>
        /// 获取棋子类型
        /// </summary>
        public PieceKind Kind
        {
            get { return kind; }
        }

       
        // 棋子是否可以参与合成
        
        public bool CanSynthesize
        {
            get { return kind == PieceKind.Normal; }
        }

        /// <summary>
        /// 创建一颗棋子
        /// </summary>
        public PieceState(
            int id,
            ElementType element,
            PieceKind kind)
        {
            // 棋子编号从 1 开始。
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id),
                    "棋子编号必须大于 0。"
                );
            }

            // 棋子必须是四种有效元素之一。
            // None 不能作为实际棋子的属性。
            if (element != ElementType.Fire &&
                element != ElementType.Water &&
                element != ElementType.Wind &&
                element != ElementType.ground)
            {
                throw new ArgumentException(
                    "棋子属性必须是火、水、风或土。",
                    nameof(element)
                );
            }

            // 棋子类型必须是普通或特种。
            if (kind != PieceKind.Normal &&
                kind != PieceKind.Special)
            {
                throw new ArgumentException(
                    "棋子类型必须是普通或特种。",
                    nameof(kind)
                );
            }

            // 检查通过后，保存数据。
            this.id = id;
            this.element = element;
            this.kind = kind;
        }
    }
}

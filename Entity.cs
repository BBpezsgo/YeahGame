using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YeahGame
{
    public abstract class Entity
    {
        public Vector2 Position;
        public bool DoesExist = true;

        public abstract void Update();
        public abstract void Render();
    }
}
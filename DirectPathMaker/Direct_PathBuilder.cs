using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LordAshes
{
    public class Direct_PathBuilder
    {
        public static List<Vector3> MakePath(Vector3 source, Vector3 destination, string parameters, int frames)
        {
            List<Vector3> path = new List<Vector3>();
            Vector3 delta = (destination - source) / frames;
            for(int f=0; f<frames; f++)
            {
                path.Add(source + (f * delta));
            }
            return path;
        }
    }
}

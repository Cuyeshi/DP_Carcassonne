using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.View
{
    [System.Serializable]
    public struct MeepleNode
    {
        public int regionId;      // ID региона из нашей логики (0, 1, 2...)
        public Transform anchor;  // Точка в 3D пространстве, куда встанет моделька мипла
    }
}

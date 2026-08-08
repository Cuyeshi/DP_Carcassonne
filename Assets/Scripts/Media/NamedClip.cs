using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Media
{
    [System.Serializable]
    public struct NamedClip
    {
        public string name;
        public AudioClip clip;
    }
}

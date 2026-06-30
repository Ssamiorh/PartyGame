using UnityEngine;

namespace Game.OfficeGame
{
    public class Prop003_Generic : MonoBehaviour
    {
        [SerializeField] private E_PropClass _propClass;
        [SerializeField] private E_PropType _propType;
    }

    public enum E_PropClass
    {
        None = 0,
        Cardbox = 1,
        Chair = 2,
    }

    public enum E_PropType
    {
        None = 0,

        CardboxBig = 1,
        CardboxMedium = 2,
        CardboxSmall = 3,


        Chair1_E = 100,
        Chair1_E_var = 101,
        Chair1_N = 102,
        Chair1_S = 103,
        Chair2_E = 104,
        Chair2_E_var = 105,
        Chair2_N = 106,
        Chair2_S = 107,
        Chair3_E = 108,
        Chair3_E_var = 109,
        Chair3_N = 110,
        Chair3_S = 111,
        Chair4_E = 112,
        Chair4_E_var = 113,
        Chair4_N = 114,
        Chair4_S = 115,
        Chair5_E = 116,
        Chair5_E_var = 117,
        Chair5_N = 118,
        Chair5_S = 119,

    }
}
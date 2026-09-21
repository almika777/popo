using System.Text.Json.Serialization;

namespace Popo.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Rating
{
    D,
    C,
    CC,
    CCC,
    B_minus,
    B,
    B_plus,
    BB_minus,
    BB,
    BB_plus,
    BBB_minus,
    BBB,
    BBB_plus,
    A_minus,
    A,
    A_plus,
    AA_minus,
    AA,
    AA_plus,
    AAA_minus,
    AAA
}

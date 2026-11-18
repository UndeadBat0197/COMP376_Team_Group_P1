using UnityEngine;

// Put on a small trigger volume by a hole: one volume on the INSIDE,
// another on the OUTSIDE. Crossing them in either direction updates the area.
[RequireComponent(typeof(Collider))]
public class RespawnAreaTrigger : MonoBehaviour
{
    [Tooltip("Checked for the marker INSIDE the trunk; unchecked for OUTSIDE.")]
    public bool marksInside = true;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        NearestRespawn respawn = other.GetComponentInParent<NearestRespawn>();
        if (respawn != null)
            respawn.SetInside(marksInside);
    }
}

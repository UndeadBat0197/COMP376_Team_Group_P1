using UnityEditor;
using UnityEngine;

// Adds draggable handles to Curved Lasers in the Scene view (Arc mode):
//   green dot  = start end   -> drag to lengthen/shorten that side only
//   red dot    = finish end  -> drag to lengthen/shorten that side only
//   yellow dot = middle      -> drag to slide the whole arc around the cylinder
// Lives in an "Editor" folder, so it never ends up in a build.
[CustomEditor(typeof(CurvedLaser))]
public class CurvedLaserEditor : Editor
{
    // Shows a note at the top of the Inspector. If you can see this note,
    // the editor script is working.
    public override void OnInspectorGUI()
    {
        var laser = (CurvedLaser)target;
        if (laser.shape == CurvedLaser.Shape.Arc)
            EditorGUILayout.HelpBox(
                "Scene view: drag GREEN (start) or RED (end) dot to change one side, " +
                "YELLOW dot to slide the whole arc. Hold Ctrl to snap.",
                MessageType.Info);
        else
            EditorGUILayout.HelpBox(
                "Drag the child Point objects with the Move tool (W). " +
                "Right-click this component's header > Add Point to add more.",
                MessageType.Info);

        DrawDefaultInspector();
    }

    void OnSceneGUI()
    {
        var laser = (CurvedLaser)target;
        if (laser.shape != CurvedLaser.Shape.Arc)
            return;

        // --- Start end ---
        float newStart = DragAngle(laser, laser.startAngle, Color.green, "Start");
        if (!Mathf.Approximately(newStart, laser.startAngle))
        {
            Undo.RecordObject(laser, "Move Laser Start");
            laser.startAngle = Mathf.Clamp(newStart, laser.endAngle - 360f, laser.endAngle - 1f);
            EditorUtility.SetDirty(laser);
        }

        // --- End end ---
        float newEnd = DragAngle(laser, laser.endAngle, Color.red, "End");
        if (!Mathf.Approximately(newEnd, laser.endAngle))
        {
            Undo.RecordObject(laser, "Move Laser End");
            laser.endAngle = Mathf.Clamp(newEnd, laser.startAngle + 1f, laser.startAngle + 360f);
            EditorUtility.SetDirty(laser);
        }

        // --- Middle: slide the whole arc ---
        float mid = (laser.startAngle + laser.endAngle) * 0.5f;
        float newMid = DragAngle(laser, mid, Color.yellow, null);
        if (!Mathf.Approximately(newMid, mid))
        {
            Undo.RecordObject(laser, "Slide Laser");
            float delta = newMid - mid;
            laser.startAngle += delta;
            laser.endAngle += delta;
            EditorUtility.SetDirty(laser);
        }
    }

    // Draws a dot at the given angle on the arc and returns the (possibly dragged) angle.
    static float DragAngle(CurvedLaser laser, float angle, Color color, string label)
    {
        Transform t = laser.transform;
        Vector3 pos = laser.ArcPoint(angle);
        float size = HandleUtility.GetHandleSize(pos) * 0.3f;

        Handles.color = color;
        if (label != null)
            Handles.Label(pos + t.up * size * 3f, label);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(pos, size, Vector3.zero, Handles.SphereHandleCap);
        if (!EditorGUI.EndChangeCheck())
            return angle;

        // Convert the dragged position back into an angle around the laser's Z axis.
        Vector3 local = Quaternion.Inverse(t.rotation) * (moved - t.position);
        float dragged = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;

        // Keep it continuous (so 359 -> 361 instead of jumping back to 1).
        float result = angle + Mathf.DeltaAngle(angle, dragged);

        // Hold Ctrl (Cmd on Mac) to snap to 5 degree steps.
        if (Event.current.control || Event.current.command)
            result = Mathf.Round(result / 5f) * 5f;

        return result;
    }
}

using UnityEngine;

namespace KitFight
{
    /// <summary>Single shared camera that keeps both fighters on screen.</summary>
    public class FightCamera : MonoBehaviour
    {
        public float pitch = 24f;
        public float baseDistance = 7f;
        public float distancePerMeter = 0.8f;
        public float minDistance = 8f;
        public float maxDistance = 22f;
        public float smoothTime = 0.25f;
        public float lookHeight = 1.1f;

        Vector3 velocity;

        void LateUpdate()
        {
            var list = PlayerCombat.All;
            if (list.Count == 0) return;

            var mid = Vector3.zero;
            foreach (var p in list) mid += p.transform.position;
            mid /= list.Count;

            float sep = 0f;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    sep = Mathf.Max(sep, Vector3.Distance(list[i].transform.position, list[j].transform.position));

            float dist = Mathf.Clamp(baseDistance + sep * distancePerMeter, minDistance, maxDistance);
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            var focus = mid + Vector3.up * lookHeight;
            var target = focus + rot * Vector3.back * dist;

            transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime);
            transform.rotation = rot;
        }
    }
}

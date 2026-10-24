using System.Collections;
using UnityEngine;

namespace KitFight
{
    /// <summary>Static helpers for "game feel": camera shake, spark bursts, slow-motion.</summary>
    public class HitFeedback : MonoBehaviour
    {
        static HitFeedback instance;
        static Material sparkMaterial;

        static HitFeedback Instance
        {
            get
            {
                if (!instance) instance = new GameObject("HitFeedback").AddComponent<HitFeedback>();
                return instance;
            }
        }

        void OnDisable()
        {
            if (instance == this) { instance = null; Time.timeScale = 1f; }
        }

        /// <summary>Impact feedback at a point. dir points away from the attacker.</summary>
        public static void Hit(Vector3 point, Vector3 dir, float damage, bool blocked)
        {
            float power = Mathf.Clamp01(damage / 24f);
            if (blocked)
            {
                FightCamera.Shake(0.03f + 0.03f * power, 0.1f);
                Sparks(point, dir, new Color(0.7f, 0.85f, 1f), 10, 4f);
            }
            else
            {
                FightCamera.Shake(0.06f + 0.14f * power, 0.15f + 0.1f * power);
                Sparks(point, dir, new Color(1f, 0.55f + 0.3f * (1f - power), 0.15f), 14 + Mathf.RoundToInt(16 * power), 5f + 3f * power);
            }
        }

        /// <summary>Golden burst + short slow-mo for a successful parry.</summary>
        public static void Parry(Vector3 point, Vector3 dir)
        {
            FightCamera.Shake(0.1f, 0.18f);
            Sparks(point, -dir, new Color(1f, 0.85f, 0.35f), 36, 7f);
            SlowMo(0.5f, 0.2f);
        }

        /// <summary>Green sparkle rising from a picked-up health potion.</summary>
        public static void Heal(Vector3 point) => Sparks(point, Vector3.up, new Color(0.35f, 1f, 0.5f), 24, 3f, -0.4f);

        /// <summary>Burst around a fighter when a charged special attack is released.</summary>
        public static void SpecialRelease(Vector3 point, Vector3 dir, Color color)
        {
            FightCamera.Shake(0.12f, 0.2f);
            Sparks(point, dir, color, 40, 9f);
        }

        public static void SlowMo(float scale, float realSeconds) => Instance.StartCoroutine(Instance.SlowMoRoutine(scale, realSeconds));

        IEnumerator SlowMoRoutine(float scale, float realSeconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            Time.timeScale = 1f;
        }

        static Material SparkMaterial()
        {
            if (sparkMaterial) return sparkMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            sparkMaterial = new Material(shader);
            if (sparkMaterial.HasProperty("_BaseColor")) sparkMaterial.SetColor("_BaseColor", Color.white);
            return sparkMaterial;
        }

        static void Sparks(Vector3 point, Vector3 dir, Color color, int count, float speed, float gravity = 1.6f)
        {
            var go = new GameObject("HitSparks");
            go.SetActive(false);
            go.transform.position = point;
            if (dir.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(dir);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.05f;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = SparkMaterial();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.08f;
            rend.lengthScale = 2f;

            go.SetActive(true);
            Destroy(go, 1f);
        }
    }
}

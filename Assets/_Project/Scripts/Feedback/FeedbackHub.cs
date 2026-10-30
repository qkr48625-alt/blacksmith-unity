using Unity.Cinemachine;
using UnityEngine;

namespace Blacksmith.Feedback
{
    // 타격 · 불티 · 소리 · 화면 흔들림을 한곳에서 부른다. 작업대 코드는 "잘 맞았다(0~100)"만 알리면 된다.
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class FeedbackHub : MonoBehaviour
    {
        public static FeedbackHub Instance { get; private set; }

        [SerializeField] Material sparkMaterial;

        CinemachineImpulseSource _impulse;
        ParticleSystem _sparks, _whiteSparks, _steam;
        AudioClip _clankBright, _clankDull, _thud, _whoosh, _sizzle, _crackle, _chime;
        float _crackleCooldown;

        void Awake()
        {
            Instance = this;
            _impulse = GetComponent<CinemachineImpulseSource>();
            _sparks = MakeSparks("Sparks", new Color(1f, 0.55f, 0.15f) * 6f, 0.9f, 4.5f);
            _whiteSparks = MakeSparks("BurnSparks", new Color(1f, 0.95f, 0.85f) * 8f, 0.5f, 2.5f);
            _steam = MakeSteam();
            _clankBright = SfxSynth.Clank(1f);
            _clankDull = SfxSynth.Clank(0.2f);
            _thud = SfxSynth.Thud();
            _whoosh = SfxSynth.Whoosh();
            _sizzle = SfxSynth.Sizzle();
            _crackle = SfxSynth.Crackle();
            _chime = SfxSynth.Chime();
        }

        void Update()
        {
            if (_crackleCooldown > 0f) _crackleCooldown -= Time.deltaTime;
        }

        // 망치 타격. quality 0~100: 잘 맞을수록 불티가 많고 소리가 맑고 화면이 더 울린다.
        public void Hit(Vector3 pos, int quality)
        {
            float q = quality / 100f;
            Burst(_sparks, pos, Mathf.RoundToInt(Mathf.Lerp(4, 60, q)));
            Play(q >= 0.6f ? _clankBright : q >= 0.25f ? _clankDull : _thud, pos, Mathf.Lerp(0.5f, 1f, q), Random.Range(0.95f, 1.06f));
            _impulse.GenerateImpulse(Mathf.Lerp(0.15f, 0.6f, q));
        }

        public void Whoosh(Vector3 pos, float strength) => Play(_whoosh, pos, Mathf.Clamp01(strength) * 0.8f, Random.Range(0.9f, 1.1f));

        public void Steam(Vector3 pos)
        {
            Burst(_steam, pos, 40);
            Play(_sizzle, pos, 0.9f, 1f);
        }

        // 쇠가 타는 동안 계속 부른다(내부에서 간격을 둔다)
        public void Burning(Vector3 pos)
        {
            if (_crackleCooldown > 0f) return;
            _crackleCooldown = 0.12f;
            Burst(_whiteSparks, pos, 6);
            Play(_crackle, pos, 0.7f, Random.Range(0.8f, 1.3f));
        }

        public void Done(Vector3 pos) => Play(_chime, pos, 0.8f, 1f);

        // 곧 무언가 바뀐다는 가벼운 "띵"(풀무질 금색 칸이 옮겨 가기 전)
        public void Cue(Vector3 pos) => Play(_clankBright, pos, 0.35f, 1.7f);

        // 숫돌에 칼날이 갈림(계속 부른다). strength 0~1 = 불티 양
        public void Grind(Vector3 pos, float strength)
        {
            Burst(_sparks, pos, Mathf.RoundToInt(Mathf.Lerp(0, 6, strength)));
            if (_crackleCooldown > 0f || strength < 0.2f) return;
            _crackleCooldown = 0.1f;
            Play(_crackle, pos, 0.25f + 0.3f * strength, Random.Range(1.4f, 1.8f));
        }

        // 쇠꼬챙이로 배출구를 찌름(둔한 소리 + 작은 불티)
        public void Poke(Vector3 pos)
        {
            Burst(_sparks, pos, 6);
            Play(_thud, pos, 0.8f, Random.Range(1.1f, 1.25f));
        }

        // 녹은 슬래그가 쏟아짐(지글거림 + 불티)
        public void Pour(Vector3 pos)
        {
            Burst(_sparks, pos, 30);
            Play(_crackle, pos, 0.8f, 0.7f);
            Play(_sizzle, pos, 0.35f, 0.6f);
        }

        static void Burst(ParticleSystem ps, Vector3 pos, int count)
        {
            ps.transform.position = pos;
            ps.Emit(count);
        }

        static void Play(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            var go = new GameObject("sfx");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 0.5f;
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        ParticleSystem MakeSparks(string name, Color color, float life, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.4f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = color;
            main.gravityModifier = 1.2f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.03f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkMaterial;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
            return ps;
        }

        ParticleSystem MakeSteam()
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.35f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial;
            return ps;
        }
    }
}

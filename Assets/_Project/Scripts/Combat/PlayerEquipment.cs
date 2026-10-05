using StarTrek.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTrek.Combat
{
    /// <summary>
    /// What the cadet has in hand: nothing, the hand phaser [F] or the tricorder [T]. Fires the
    /// phaser [click], switches stun/kill [X], and shows the held item in front of the camera.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        public enum Item { None, Phaser, Tricorder }

        [SerializeField] InputActionAsset actions;
        [SerializeField] HandPhaser phaser;
        [SerializeField] GameObject phaserModel;
        [SerializeField] Tricorder tricorder;
        [SerializeField] GameObject tricorderModel;
        [SerializeField] bool phaserAllowed = true;
        [SerializeField] bool tricorderAllowed = true;

        InputAction fire, phaserKey, tricorderKey, settingKey;
        FirstPersonController body;
        Vector3 phaserRest, tricorderRest;
        float kick;
        GUIStyle hint;
        int styledForHeight;

        public Item Current { get; private set; }
        public HandPhaser Phaser => phaser;
        public Tricorder Tricorder => tricorder;
        public bool PhaserAllowed { get => phaserAllowed; set { phaserAllowed = value; if (!value && Current == Item.Phaser) Equip(Item.None); } }
        public bool TricorderAllowed { get => tricorderAllowed; set { tricorderAllowed = value; if (!value && Current == Item.Tricorder) Equip(Item.None); } }

        void Awake()
        {
            var map = actions.FindActionMap("Player", true);
            fire = map.FindAction("Fire", true);
            phaserKey = map.FindAction("Phaser", true);
            tricorderKey = map.FindAction("Tricorder", true);
            settingKey = map.FindAction("PhaserSetting", true);
            body = GetComponent<FirstPersonController>();
            if (phaserModel != null) phaserRest = phaserModel.transform.localPosition;
            if (tricorderModel != null) tricorderRest = tricorderModel.transform.localPosition;
            Equip(Item.None);
        }

        public void Equip(Item item)
        {
            if (item == Item.Phaser && !phaserAllowed || item == Item.Tricorder && !tricorderAllowed)
                item = Item.None;
            Current = item;
            if (phaserModel != null) phaserModel.SetActive(item == Item.Phaser);
            if (tricorderModel != null) tricorderModel.SetActive(item == Item.Tricorder);
            if (tricorder != null) tricorder.Open = item == Item.Tricorder;
        }

        void Update()
        {
            if (body != null && !body.InputEnabled)
                return;
            if (phaserKey.WasPressedThisFrame())
                Equip(Current == Item.Phaser ? Item.None : Item.Phaser);
            else if (tricorderKey.WasPressedThisFrame())
                Equip(Current == Item.Tricorder ? Item.None : Item.Tricorder);

            if (Current == Item.Phaser && phaser != null)
            {
                if (settingKey.WasPressedThisFrame())
                    phaser.ToggleSetting();
                if (fire.WasPressedThisFrame() && phaser.Fire())
                    kick = 1f;
            }

            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 6f);
            float bob = Mathf.Sin(Time.time * 1.7f) * 0.003f;
            if (phaserModel != null)
                phaserModel.transform.localPosition = phaserRest + new Vector3(0f, bob, -0.03f * kick);
            if (tricorderModel != null)
                tricorderModel.transform.localPosition = tricorderRest + new Vector3(0f, bob, 0f);
        }

        void OnGUI()
        {
            if (Current != Item.Phaser || phaser == null)
                return;
            if (hint == null || styledForHeight != Screen.height)
            {
                styledForHeight = Screen.height;
                hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.019f)), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, richText = true };
            }
            bool stun = phaser.Setting == PhaserSetting.Stun;
            string text = stun ? "PHASER  <color=#8cc8ff>STUN</color>   [X]" : "PHASER  <color=#ff7050>KILL</color>   [X]";
            var r = new Rect(0f, Screen.height * 0.9f, Screen.width * 0.97f, hint.fontSize * 2f);
            hint.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), stun ? "PHASER  STUN   [X]" : "PHASER  KILL   [X]", hint);
            hint.normal.textColor = new Color(1f, 0.85f, 0.6f);
            GUI.Label(r, text, hint);
        }
    }
}

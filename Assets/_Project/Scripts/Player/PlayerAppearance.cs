using Mirror;
using UnityEngine;

namespace Steading.Player
{
    public class PlayerAppearance : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnNameChanged))] private string characterName = CharacterCustomization.Default.characterName;
        [SyncVar(hook = nameof(OnColorChanged))] private Color skinColor = CharacterCustomization.Default.skinColor;
        [SyncVar(hook = nameof(OnColorChanged))] private Color hairColor = CharacterCustomization.Default.hairColor;
        [SyncVar(hook = nameof(OnColorChanged))] private Color tunicColor = CharacterCustomization.Default.tunicColor;
        [SyncVar(hook = nameof(OnColorChanged))] private Color pantsColor = CharacterCustomization.Default.pantsColor;
        [SyncVar(hook = nameof(OnColorChanged))] private Color cloakColor = CharacterCustomization.Default.cloakColor;
        [SyncVar(hook = nameof(OnFloatChanged))] private float heightScale = CharacterCustomization.Default.heightScale;
        [SyncVar(hook = nameof(OnFloatChanged))] private float buildScale = CharacterCustomization.Default.buildScale;
        [SyncVar(hook = nameof(OnBoolChanged))] private bool beardEnabled = CharacterCustomization.Default.beardEnabled;
        [SyncVar(hook = nameof(OnBoolChanged))] private bool helmetEnabled = CharacterCustomization.Default.helmetEnabled;

        private PlayerAnimatorBridge _visualAnimator;

        public string CharacterName => characterName;

        private void Awake()
        {
            _visualAnimator = GetComponent<PlayerAnimatorBridge>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyCurrentCustomization();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            var local = CharacterCustomization.LoadLocal();
            CmdApplyCustomization(
                local.characterName,
                local.skinColor,
                local.hairColor,
                local.tunicColor,
                local.pantsColor,
                local.cloakColor,
                local.heightScale,
                local.buildScale,
                local.beardEnabled,
                local.helmetEnabled);
        }

        [Command]
        private void CmdApplyCustomization(
            string requestedName,
            Color requestedSkin,
            Color requestedHair,
            Color requestedTunic,
            Color requestedPants,
            Color requestedCloak,
            float requestedHeight,
            float requestedBuild,
            bool requestedBeard,
            bool requestedHelmet)
        {
            var sanitized = new CharacterCustomization
            {
                characterName = requestedName,
                skinColor = requestedSkin,
                hairColor = requestedHair,
                tunicColor = requestedTunic,
                pantsColor = requestedPants,
                cloakColor = requestedCloak,
                heightScale = requestedHeight,
                buildScale = requestedBuild,
                beardEnabled = requestedBeard,
                helmetEnabled = requestedHelmet
            }.Sanitized();

            characterName = sanitized.characterName;
            skinColor = sanitized.skinColor;
            hairColor = sanitized.hairColor;
            tunicColor = sanitized.tunicColor;
            pantsColor = sanitized.pantsColor;
            cloakColor = sanitized.cloakColor;
            heightScale = sanitized.heightScale;
            buildScale = sanitized.buildScale;
            beardEnabled = sanitized.beardEnabled;
            helmetEnabled = sanitized.helmetEnabled;
            ApplyCurrentCustomization();
        }

        private void ApplyCurrentCustomization()
        {
            if (_visualAnimator == null) _visualAnimator = GetComponent<PlayerAnimatorBridge>();
            if (_visualAnimator == null) return;

            _visualAnimator.ApplyCustomization(BuildCustomization());
        }

        private CharacterCustomization BuildCustomization()
        {
            return new CharacterCustomization
            {
                characterName = characterName,
                skinColor = skinColor,
                hairColor = hairColor,
                tunicColor = tunicColor,
                pantsColor = pantsColor,
                cloakColor = cloakColor,
                heightScale = heightScale,
                buildScale = buildScale,
                beardEnabled = beardEnabled,
                helmetEnabled = helmetEnabled
            }.Sanitized();
        }

        private void OnNameChanged(string oldValue, string newValue)
        {
            ApplyCurrentCustomization();
        }

        private void OnColorChanged(Color oldValue, Color newValue)
        {
            ApplyCurrentCustomization();
        }

        private void OnFloatChanged(float oldValue, float newValue)
        {
            ApplyCurrentCustomization();
        }

        private void OnBoolChanged(bool oldValue, bool newValue)
        {
            ApplyCurrentCustomization();
        }
    }
}

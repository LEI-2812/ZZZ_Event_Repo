using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

namespace BeastBeat
{
    // 메인 화면의 진행 상태를 초기화합니다. 이동은 Move_Scene에 위임합니다.
    public sealed class MainSceneActions : MonoBehaviour
    {



        TMP_Text levelLabel;
        string levelTemplate;
        int displayedLevel = -1;

        // 씬의 글자 스타일과 LV. 접두사는 유지하고 99 자리만 실제 레벨로 바꿉니다.
        public void RefreshLevel()
        {
            if (!levelLabel)
            {
                levelLabel = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                    .FirstOrDefault(text => text.name == "Txt_Lv");
                if (!levelLabel) return;
                levelTemplate = levelLabel.text;
            }
            var progress = BeastBeatSession.Progress;
            if (progress == null || displayedLevel == progress.Save.level) return;
            displayedLevel = progress.Save.level;
            levelLabel.text = levelTemplate.Replace("99", displayedLevel.ToString("D2"));
        }
        Image playerImage;
        Sprite malePortrait, femalePortrait;
        string playerRevision;
        float nextPlayerRefresh;

        public void RefreshPlayer()
        {
            if (!playerImage)
                playerImage = gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Image>(true))
                    .FirstOrDefault(image => image.name == "Img_Player");
            if (!playerImage) return;
            var book = GameWorkbook.Load();
            if (!book || playerRevision == book.revision) return;
            playerRevision = book.revision;
            try
            {
                int gender = RewardCatalog.ReadPlayerGender(book);
                playerImage.sprite = PlayerPortrait(gender);
                playerImage.color = Color.white;
                playerImage.type = Image.Type.Simple;
                playerImage.preserveAspect = true;
                if (BeastBeatSession.Progress != null)
                    BeastBeatSession.Progress.Data.rewardCatalog.user.gender = gender;
            }
            catch (System.Exception ex) { Debug.LogError("플레이어 성별: " + ex.Message, this); }
        }
        void Update()
        {
            RefreshLevel();
            if (Time.unscaledTime < nextPlayerRefresh) return;
            nextPlayerRefresh = Time.unscaledTime + .5f;
            RefreshPlayer();
        }

        void Awake()
        {
            if (!enabled) return;
            EnsureProgress();
            RefreshPlayer();
            RefreshLevel();
        }
        public Sprite PlayerPortrait(int gender)
        {
            var cached = gender == 1 ? malePortrait : femalePortrait;
            if (cached) return cached;
            string path = gender == 1 ? "Image/wise" : "Image/belle";
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) throw new System.InvalidOperationException("플레이어 이미지 누락: " + path);
            // Square head portraits, expressed relative to the source image (top-left origin).
            var crop = gender == 1
                ? new Rect(90f / 446f, 0, 180f / 446f, 180f / 1174f)
                : new Rect(175f / 652f, 0, 340f / 652f, 340f / 2000f);
            var pixels = new Rect(crop.x * texture.width,
                (1 - crop.y - crop.height) * texture.height,
                crop.width * texture.width, crop.height * texture.height);
            var sprite = Sprite.Create(texture, pixels, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = gender == 1 ? "Wise Face" : "Belle Face";
            if (gender == 1) malePortrait = sprite; else femalePortrait = sprite;
            return sprite;
        }
        void OnDestroy()
        {
            if (malePortrait) Destroy(malePortrait);
            if (femalePortrait) Destroy(femalePortrait);
        }
        void EnsureProgress()
        {
            if(BeastBeatSession.Progress!=null)return;
            var progress=new ProgressService(GameData.Load());
            BeastBeatSession.Progress=progress;
            var stage=progress.Data.ResolveStage(progress.Save.lastStage);
            BeastBeatSession.SelectedStage=stage==null?0:stage.id;
            BeastBeatSession.StageGroup=stage==null?1:stage.type;
        }
    }
}

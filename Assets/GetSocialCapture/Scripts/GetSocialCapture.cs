using System;
using System.Collections;
using System.IO;
using GetSocialSdk.Capture.Scripts.Internal.Gif;
using GetSocialSdk.Capture.Scripts.Internal.Recorder;
using GetSocialSdk.Scripts.Internal.Util;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using ThreadPriority = System.Threading.ThreadPriority;
using UnityEngine.Networking;
namespace GetSocialSdk.Capture.Scripts
{
    
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public class GetSocialCapture : MonoBehaviour
    {
        IEnumerator isTextureRecordFinish()
        {
            yield return new WaitForSeconds(7f);
            allTextures = Resources.LoadAll<Texture2D>("vid"); // resources klasörünün vid adlı dosyasının içindekilerinin hepsini yüklüyoruz
            textures.texture= allTextures[textCountValue]; // texturesi vid dosyasının altındaki ilk klibe eşitliyoruz.
            textCountValue++;
            StartCoroutine(UploadMultipleFiles());
        }
        public Texture2D[] allTextures;
        public RawImage textures;
        int textCountValue = 0;
        public GetSocialCapture capture;
        public GetSocialCapturePreview capturePreview;

        // start recording if something interesting happens in the game
        public void RecordAction() // burda video kaydını başlatan fonksiyonumuz
        {
            //capture2.StopCapture();
            //capturePreview2.Stop();
            capturePreview.Stop(); // Görüntü oynatılmasını durdurur
            capturePreview.GetComponent<RawImage>().texture = null; // verilen texturenin görüntüsünü kaldırır
            capturePreview._framesToPlay.Clear(); // kayıt edilen frame'i temizler
            capturePreview._play = true; // tekrar play etmek için true veriyoruz
            capturePreview._previewInitialized = false; // baştan initialized etmek için false değeri döndürüyoruz.
            capture.StartCapture();
        }

        // stop recording
        public void ActionFinished()
        {
            capture.StopCapture(); // kaydı durduruyoruz
            capturePreview.Play(); // görüntüyü başlatıyoruz
            StartCoroutine(isTextureRecordFinish());
            capture.GenerateCapture(result =>
            {
                // use gif, like send it to your friends by using GetSocial Sdk
            });
        }
        /// <summary>
        /// Defines how frames are captured.
        /// </summary>
        public enum GetSocialCaptureMode
        {
            /// <summary>
            /// Frames captured continuously with the give frame rate.
            /// </summary>
            Continuous = 0,
            
            /// <summary>
            /// CaptureFrame() has to be called to make a capture.
            /// </summary>
            Manual
        }
    
        #region Public fields

        /// <summary>
        /// Number of captured frames per second. Default is 10.
        /// </summary>
        public int captureFrameRate = 10;

        /// <summary>
        /// Capture mode.
        /// </summary>
        public GetSocialCaptureMode captureMode = GetSocialCaptureMode.Continuous;

        /// <summary>
        /// Max. number of captured frames during the session. Default is 50.
        /// </summary>
        public int maxCapturedFrames = 50;

        /// <summary>
        /// Number of displayed frames per second. Default is 30.
        /// </summary>
        public int playbackFrameRate = 30;

        /// <summary>
        /// Generated gif loops or played only once.
        /// </summary>
        public bool loopPlayback = true;

        /// <summary>
        /// Captured content.
        /// </summary>
        public Camera capturedCamera;
        
        #endregion

        #region Private variables

        private string _captureId;
        private string _resultFilePath;
        private float _elapsedTime;
        private Recorder _recorder;

        private const string GeneratedContentFolderName = "vid"; //oluşturulan içerik klasörünün adını veriyoruz benimki Resource klasörü altına vid adlı klasördü

        #endregion

        #region Public methods

        public void StartCapture()
        {
            if (_captureId != null)
            {
                CleanUp();
            }
            InitSession();
            _recorder.CurrentState = Recorder.RecordingState.Recording;
        }

        public void StopCapture()
        {
            _recorder.CurrentState = Recorder.RecordingState.OnHold;
        }

        public void ResumeCapture()
        {
            if (_captureId == null)
            {
                Debug.Log("There is no previous capture session to continue");
            }
            else
            {
                _recorder.CurrentState = Recorder.RecordingState.Recording;
            }
        }

        public void CaptureFrame()
        {
            if (_captureId == null)
            {
                InitSession();                                
            }
            _recorder.CurrentState = Recorder.RecordingState.RecordNow;
        }

        public void GenerateCapture(Action<byte[]> result)
        {
            _recorder.CurrentState = Recorder.RecordingState.OnHold;
            if (StoreWorker.Instance.StoredFrames.Count() > 0)
            {
                var generator = new GeneratorWorker(loopPlayback, playbackFrameRate, ThreadPriority.BelowNormal, StoreWorker.Instance.StoredFrames,
                     _resultFilePath,
                    () =>
                    {
                        Debug.Log("Result: " + _resultFilePath);
                        
                        MainThreadExecutor.Queue(() => {
                            result(File.ReadAllBytes(_resultFilePath));
                            
                        });
                    });
                generator.Start();
            }
            else
            {
                Debug.Log("Something went wrong, check your settings");
                result(new byte[0]);
            }
        }

        #endregion

        #region Unity methods

        private void Awake()
        {
            if (capturedCamera == null)
            {
                capturedCamera = GetComponent<Camera>();
            }

            if (capturedCamera == null)
            {
                Debug.LogError("Camera is not set");
                return;
            }
            _recorder = capturedCamera.GetComponent<Recorder>(); 
            if (_recorder == null)
            {
                _recorder = capturedCamera.gameObject.AddComponent<Recorder>();
            }

            _recorder.CaptureFrameRate = captureFrameRate;
        }

        private void OnDestroy()
        {
            StoreWorker.Instance.Clear();
        }

        #endregion

        #region Private methods

        private static string GetResultDirectory()
        {
            string resultDirPath;
            #if UNITY_EDITOR
                resultDirPath = Application.dataPath; 
            #else
                resultDirPath = Application.persistentDataPath; 
            #endif
            resultDirPath += "\\Resources" + Path.DirectorySeparatorChar + GeneratedContentFolderName; // resultDirPath yoluna nereye ulaşacağını veriyoruz 
            if (!Directory.Exists(resultDirPath))
            {
                Directory.CreateDirectory(resultDirPath);
            }
            return resultDirPath;
        }

        private void InitSession()
        {
            _captureId = Guid.NewGuid().ToString();
            var fileName = string.Format("result"+ textCountValue + ".gif", _captureId); // ".gif" kısmında istediğimiz türde obje yaratmak içni veriyoruz 
            _resultFilePath = GetResultDirectory() + Path.DirectorySeparatorChar + fileName;
            StoreWorker.Instance.Start(ThreadPriority.BelowNormal, maxCapturedFrames);
            //textCountValue++;
        }
        
        private void CleanUp()
        {
            if (File.Exists(_resultFilePath))
            {
                File.Delete(_resultFilePath);
            }
        }

        #endregion
       IEnumerator UploadMultipleFiles()
        {
            if (Resources.LoadAll<Texture2D>("vid") != null)
            {
                yield return new WaitForSeconds(2f);                                                // 2 saniye bekleme süresini video kayıt etsin diye bekletmek için veriyoruz.
                string[] path = new string[3];                                                      // path dizinini çekmemize yarar
                path[0] = "C:/Users/berkc/Desktop/Record/Recordig/Assets/Resources/vid/result0.gif";// Dizinin olduğu konumu path[0] a eşitler 



                if (!File.Exists(path[0]))                                                      // yükleme dosyasının var olup olmadığını kontrol edin, aksi takdirde hata döner.
                {
                    Debug.Log("ERROR! Can't locate the file to upload: " + path[0]);
                    yield break;
                }
                byte[] localFile = File.ReadAllBytes(path[0]);                                  // Varsa tüm dosyanın bytelerini bir diziye okutuyoruz
                yield return localFile;                                                         // yerel dosyanın yüklenmesi bitene kadar bekleyin

                WWWForm form = new WWWForm();                                                   // bir web formu hazırlar - tüm verileri yüklemek için kullanılır
                form.AddBinaryData("file", localFile, path[0]);                                 // yerel dosya verilerini forma kopyalayın/ekleyin ve dosyaya bir ad verin

                UnityWebRequest req = UnityWebRequest.Post("https://www.ndgstudio.com.tr/berkcan/StreamingAssets", form);// url = http://yourserver.com/upload.php
                yield return req.SendWebRequest();                                              // formu gönderin ve sunucuyu yönetmek için PHP'yi arayın ve tamamlanana kadar bekleyin

                if (req.isHttpError || req.isNetworkError)                                      // hata varsa burası dönecektir
                    Debug.Log(req.error);
                else
                    Debug.Log("SUCCESS! File uploaded: " + path[0]);                            // hata yoksa yüklendi başarılı yazısı döndürecektir.
            }
            
        }
    }
}    

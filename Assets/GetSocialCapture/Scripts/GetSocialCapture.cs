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
        IEnumerator isVideoRecordFinish()
        {
            yield return new WaitForSeconds(7f);
            allVideos = Resources.LoadAll<Texture2D>("vid"); // resources klasörünün vid adlı dosyasının içindekilerinin hepsini yüklüyoruz
            videoPlayer.texture= allVideos[videoCountValue]; // videoPlayeri vid dosyasının altındaki ilk klibe eşitliyoruz.
            //videoCountValue++;
            StartCoroutine(UploadMultipleFiles());
        }
        public Texture2D[] allVideos;
        public RawImage videoPlayer;
        int videoCountValue = 0;
        public GetSocialCapture capture2;
        public GetSocialCapturePreview capturePreview2;
        public void ActionFinished2()
        {
            //capturePreview2.
            capture2.StopCapture();
            // show preview
            capturePreview2.Play();
            StartCoroutine(isVideoRecordFinish());
        }
        public GetSocialCapture capture;


        // start recording if something interesting happens in the game
        public void RecordAction()
        {
            capture.StartCapture();
        }

        // stop recording
        public void ActionFinished()
        {
            capture.StopCapture();
            // generate gif
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

        private const string GeneratedContentFolderName = "vid";

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
            resultDirPath += "\\Resources" + Path.DirectorySeparatorChar + GeneratedContentFolderName;
            if (!Directory.Exists(resultDirPath))
            {
                Directory.CreateDirectory(resultDirPath);
            }
            return resultDirPath;
        }

        private void InitSession()
        {
            _captureId = Guid.NewGuid().ToString();
            var fileName = string.Format("result"+ videoCountValue + ".gif", _captureId);
            _resultFilePath = GetResultDirectory() + Path.DirectorySeparatorChar + fileName;
            StoreWorker.Instance.Start(ThreadPriority.BelowNormal, maxCapturedFrames);
            //videoCountValue++;
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
            yield return new WaitForSeconds(2f);
            print("asd");
            string[] path = new string[3];                                                      // obviously there are better ways to set up your file list
            path[0] = "C:/Users/berkc/Desktop/Record/Recordig/Assets/Resources/vid/result0.gif";// Directory.Getfiles()



            if (!File.Exists(path[0]))                                                      // check the upload file exists else quit with error
            {
                Debug.Log("ERROR! Can't locate the file to upload: " + path[0]);
                yield break;
            }
            byte[] localFile = File.ReadAllBytes(path[0]);                                  // IMPORTANT: if it does exist read all the file bytes into an array
            yield return localFile;                                                         // wait until the local file has finished loading

            WWWForm form = new WWWForm();                                                   // get a web form ready - used to upload all data
            form.AddBinaryData("file", localFile, path[0]);                                 // copy/attach the local file data to the form and give the file a name

            UnityWebRequest req = UnityWebRequest.Post("https://drive.google.com/drive/my-drive", form);// url = http://yourserver.com/upload.php
            yield return req.SendWebRequest();                                              // post the form and call the PHP to manage the server and wait until complete

            if (req.isHttpError || req.isNetworkError)                                      // trap and report any errors 
                Debug.Log(req.error);
            else
                Debug.Log("SUCCESS! File uploaded: " + path[0]);                            // else the file uploaded ok
        }
    }
}    
 
       
    





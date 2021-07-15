using agora_gaming_rtc;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using Logger = agora_utilities.Logger;

public class TranscodingApp : PlayerViewControllerBase
{
    string YTURL = "rtmp://a.rtmp.youtube.com/live2/";// h8sf-wfkf-3bcj-zwdq-8bse";

    int hostCount = 0;
    uint MyUID { get; set; }
    uint RemoteUID { get; set; }
    bool IsStreamingLive { get; set; }
    Logger logger = null;
    MonoBehaviour monoProxy;
    Texture2D mTexture;
    
    //Rect mRect;
    bool running = false;
    int timestamp = 0;
    Vector2Int resolution;
    Texture2D newTexture;
    protected override void PrepareToJoin()
    {

        base.PrepareToJoin();
        
        resolution = GetResolution();
        Debug.LogError("New Resolution: " + resolution);
        EnableShareScreen();
        VideoEncoderConfiguration configuration = new VideoEncoderConfiguration
        {
            dimensions = new VideoDimensions() { width = resolution.x, height = resolution.y },
            frameRate = FRAME_RATE.FRAME_RATE_FPS_24,
            mirrorMode = VIDEO_MIRROR_MODE_TYPE.VIDEO_MIRROR_MODE_ENABLED
        };
        mRtcEngine.SetVideoEncoderConfiguration(configuration);
        ClientRoleOptions option = new ClientRoleOptions();
        option.audienceLatencyLevel = AUDIENCE_LATENCY_LEVEL_TYPE.AUDIENCE_LATENCY_LEVEL_LOW_LATENCY;
        mRtcEngine.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER, option);
        mRtcEngine.OnFirstLocalVideoFrame = delegate (int width, int height, int elapsed)
        {
            Debug.LogFormat("OnFirstLocalVideoFrame => width:{0} height:{1} elapsed:{2}", width, height, elapsed);
        };
        mRtcEngine.OnFirstRemoteVideoFrame = delegate (uint uid, int width, int height, int elapsed)
        {
            Debug.LogFormat("OnFirstRemoteVideoFrame => width:{0} height:{1} elapsed:{2} uid:{3}", width, height, elapsed, uid);
        };
        mRtcEngine.OnStreamPublished = OnStreamPublished;
    }

    void EnableShareScreen()
    {
        // Very Important to make this app work
        mRtcEngine.SetExternalVideoSource(true, false);
    }

    protected override void SetupUI()
    {
        base.SetupUI();
        monoProxy = GameObject.Find("Canvas").GetComponent<MonoBehaviour>();
        Button openButton = GameObject.Find("OpenStreaming").GetComponent<Button>();
        openButton.onClick.AddListener(() => { HandleOpenButtonClick(openButton); });
        
        GameObject loggerObj = GameObject.Find("LoggerText");
        if (loggerObj != null)
        {
            Text text = loggerObj.GetComponent<Text>();
            if (text != null)
            {
                logger = new Logger(text);
                logger.Clear();
            }
        }
    }

    void HandleOpenButtonClick(Button button)
    {   
        if (IsStreamingLive)
        {
            StopTranscoding();
            button.GetComponentInChildren<Text>().text = "Start";
        }
        else
        {
            button.GetComponentInChildren<Text>().text = "Stop";
            GameObject obj = GameObject.Find("PanelContainer");
            Debug.LogError("Obj :" + obj.gameObject.name);
            GameObject container = obj.transform.GetChild(0).gameObject;
            container.SetActive(true);
            Button startButton = GameObject.Find("StartButton").GetComponent<Button>();
            startButton.onClick.AddListener(() => { HandleStartButtonClick(startButton, container); });
        }

    }

    void HandleStartButtonClick(Button button, GameObject container)
    {
        if (IsStreamingLive)
        {
            StopTranscoding();
            button.GetComponentInChildren<Text>().text = "Start";
            container.SetActive(false);
        }
        else
        {
            GameObject obj = GameObject.Find("RTMPKeyInput");
            Debug.LogError("Name: " + obj.name);
            InputField rtmpKey = GameObject.Find("RTMPKeyInput").GetComponent<InputField>();
            YTURL += rtmpKey.text;
            Debug.LogError("Youtube: " + YTURL);
            container.SetActive(false);
            StartSharing();
            if (logger != null)
            {
                logger.DebugAssert(IsCDNAddressReady(), "You may need to fill in your Stream Key in the TranscodingApp source file!");
            }
            button.GetComponentInChildren<Text>().text = "Stop";
            StartTranscoding(RemoteUID);
        }
        IsStreamingLive = !IsStreamingLive;
    }

    protected override void OnJoinChannelSuccess(string channelName, uint uid, int elapsed)
    {
        base.OnJoinChannelSuccess(channelName, uid, elapsed);
        MyUID = uid;
    }

    protected void StartSharing()
    {
        if (running == false)
        {
            // Create a texture the size of the rectangle you just created
            mTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            mTexture.Apply();
            // get the rtc engine instance, assume it has been created before this script starts
            running = true;
            monoProxy.StartCoroutine(shareScreen());
        }
    }

    Vector2Int  GetResolution()
    {   
        if (Screen.width > 1280)
        {
            Debug.LogError("Org Width: " + Screen.width);
            float factor = 1.7f;// 1920 / 1280;
            Debug.LogError("Factor: " + factor);
            Debug.LogError("Org Height: " + Screen.height);
            int newHeight = Mathf.RoundToInt(Screen.height / factor);
            Debug.LogError("New Height: " + newHeight);
            return new Vector2Int(1280, newHeight);
        }
        return new Vector2Int(Screen.width, Screen.height);
    }

    /*public static Texture2D resizeTexture(Texture2D oldTexture, float scaleFactor)
    {
        int height = Mathf.RoundToInt(oldTexture.height * scaleFactor);
        int width = Mathf.RoundToInt(oldTexture.width * scaleFactor);
        if (scaleFactor == 1)
        {
            return oldTexture;
        }
        if (height * width > 1000000000)
        {
            Debug.LogError("you are trying to build a texture with atleast 1billion pixels, this will most likeley crash unity");
            return Texture2D.whiteTexture;
        }
        Texture2D texture = new Texture2D(height, width);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[height * width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[x + y * width] = oldTexture.GetPixelBilinear((float)x / (float)width, (float)y / (float)height);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }*/

    Texture2D Resize(Texture2D sourceTex, int Width, int Height, bool flipY)
    {
        Texture2D destTex = new Texture2D(Width, Height, sourceTex.format, false);
        Color[] destPix = new Color[Width * Height];
        int y = 0;
        while (y < Height)
        {
            int x = 0;
            while (x < Width)
            {
                float xFrac = x * 1.0F / (Width);
                float yFrac = y * 1.0F / (Height);
                if (flipY == true)
                    yFrac = (1 - y - 2) * 1.0F / (Height);
                destPix[y * Width + x] = sourceTex.GetPixelBilinear(xFrac, yFrac);
                x++;
            }
            y++;
        }
        destTex.SetPixels(destPix);
        destTex.Apply();
        return destTex;
    }


    /*public Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, true);
        Color[] rpixels = result.GetPixels(0);
        float incX = ((float)1 / source.width) * ((float)source.width / targetWidth);
        float incY = ((float)1 / source.height) * ((float)source.height / targetHeight);
        for (int px = 0; px < rpixels.Length; px++)
        {
            rpixels[px] = source.GetPixelBilinear(incX * ((float)px % targetWidth),
                              incY * ((float)Mathf.Floor(px / targetWidth)));
        }
        result.SetPixels(rpixels, 0);
        result.Apply();
        return result;
    }*/

    IEnumerator shareScreen()
    {
        while (running)
        {
            yield return new WaitForEndOfFrame();

            //Read the Pixels inside the Rectangle
            mTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
            mTexture.Apply();

            if(mTexture.width > 1280)
            {   
                newTexture = Resize(mTexture, resolution.x, resolution.y, false);
            }
            else
            {
                newTexture = mTexture;

            }

            Debug.LogError("Current Resolution: " + newTexture.width+ " H: "+ newTexture.height);
            // Get the Raw Texture data from the the from the texture and apply it to an array of bytes
            byte[] bytes = newTexture.GetRawTextureData();
            
            // Check to see if there is an engine instance already created
            //if the engine is present
            if (mRtcEngine != null)
            {
                //Create a new external video frame
                ExternalVideoFrame externalVideoFrame = new ExternalVideoFrame();
                //Set the buffer type of the video frame
                externalVideoFrame.type = ExternalVideoFrame.VIDEO_BUFFER_TYPE.VIDEO_BUFFER_RAW_DATA;
                // Set the video pixel format
                externalVideoFrame.format = ExternalVideoFrame.VIDEO_PIXEL_FORMAT.VIDEO_PIXEL_RGBA;  // V.3.x.x
                //apply raw data you are pulling from the rectangle you created earlier to the video frame
                externalVideoFrame.buffer = bytes;
                //Set the width of the video frame (in pixels)
                externalVideoFrame.stride = resolution.x;
                //Set the height of the video frame
                externalVideoFrame.height = resolution.y;
                //Rotate the video frame (0, 90, 180, or 270)
                externalVideoFrame.rotation = 180;
                externalVideoFrame.timestamp = timestamp++;
                //Push the external video frame with the frame we just created
                mRtcEngine.PushVideoFrame(externalVideoFrame);
                if (timestamp % 100 == 0)
                {
                    Debug.LogWarning("Pushed frame = " + timestamp);
                }
            }
        }
    }

    protected override void OnUserJoined(uint uid, int elapsed)
    {
        hostCount++;

        if (hostCount == 2)
        {
            RemoteUID = uid;
        }
        else
        {
            return;
        }
    }

    protected override void OnUserOffline(uint uid, USER_OFFLINE_REASON reason)
    {
        base.OnUserOffline(uid, reason);
        if (RemoteUID == uid)
        {
            //host2.SetEnable(false);
            hostCount--;
            RemoteUID = 0;
        }
    }

    void StopTranscoding()
    {
        running = false;
        mRtcEngine.RemovePublishStreamUrl(YTURL);
        IsStreamingLive = false;
    }

    void StartTranscoding(uint uid)
    {
        Debug.Log("Remote Id: " + uid);
        LiveTranscoding live = new LiveTranscoding();
        TranscodingUser user = new TranscodingUser();
        user.uid = uid;
        user.x = 0;
        user.y = 0;
        user.width = resolution.x;
        user.height = resolution.y;
        user.audioChannel = 0;
        user.alpha = 1;

        TranscodingUser me = user;
        me.uid = MyUID;
        me.x = me.width;

        live.transcodingUsers = new TranscodingUser[] { me, user };
        live.userCount = 2;

        /*live.width = Screen.width;
        live.height = Screen.height;*/
        live.width = resolution.x; //1280;
        live.height = resolution.y;// 720;
        live.videoBitrate = 400;
        live.videoCodecProfile = VIDEO_CODEC_PROFILE_TYPE.VIDEO_CODEC_PROFILE_MAIN;
        live.videoGop = 30;
        live.videoFramerate = 24;
        live.lowLatency = false;

        live.audioSampleRate = AUDIO_SAMPLE_RATE_TYPE.AUDIO_SAMPLE_RATE_44100;
        live.audioBitrate = 48;
        live.audioChannels = 1;
        live.audioCodecProfile = AUDIO_CODEC_PROFILE_TYPE.AUDIO_CODEC_PROFILE_LC_AAC;
        live.liveStreamAdvancedFeatures = new LiveStreamAdvancedFeature[0];
        mRtcEngine.SetLiveTranscoding(live);

        int rc = mRtcEngine.AddPublishStreamUrl(url: YTURL, transcodingEnabled: true);
        Debug.Assert(rc == 0, " error in adding " + YTURL);
    }

    string GetErrorCode(int code)
    {
        string error = "";
        switch (code)
        {
            case 0:
                error = "success";
                break;
            case 1:
                error = "The publishing fails";
                break;
            case 2:
                error = "ERR_INVALID_ARGUMENT";
                break;
            case 10:
                error = "The publishing fails";
                break;
            case 19:
                error = "The publishing fails";
                break;
        }
        return error;
    }

    private void OnStreamPublished(string url, int errorCode)
    {
        /** Reports the result of calling the {@link agora_gaming_rtc.IRtcEngine.AddPublishStreamUrl AddPublishStreamUrl} method. (CDN live only.)
		* 
		* @param url The RTMP URL address.
		* @param error Error code: Main errors include:
		* - ERR_OK(0): The publishing succeeds.
		* - ERR_FAILED(1): The publishing fails.
		* - ERR_INVALID_ARGUMENT(2): Invalid argument used. If, for example, you did not call {@link agora_gaming_rtc.IRtcEngine.SetLiveTranscoding SetLiveTranscoding} to configure LiveTranscoding before calling `AddPublishStreamUrl`, the SDK reports `ERR_INVALID_ARGUMENT(2)`.
		* - ERR_TIMEDOUT(10): The publishing timed out.
		* - ERR_ALREADY_IN_USE(19): The chosen URL address is already in use for CDN live streaming.
		* - ERR_RESOURCE_LIMITED(22): The backend system does not have enough resources for the CDN live streaming.
		* - ERR_ENCRYPTED_STREAM_NOT_ALLOWED_PUBLISH(130): You cannot publish an encrypted stream.
		* - ERR_PUBLISH_STREAM_CDN_ERROR(151)
		* - ERR_PUBLISH_STREAM_NUM_REACH_LIMIT(152)
		* - ERR_PUBLISH_STREAM_NOT_AUTHORIZED(153)
		* - ERR_PUBLISH_STREAM_INTERNAL_SERVER_ERROR(154)
		* - ERR_PUBLISH_STREAM_FORMAT_NOT_SUPPORTED(156)
		*/
        if (errorCode == 0)
        {
            Debug.LogError("Success");
        }
        else
        {
           
                Debug.LogError("Stream Unsuccessful: " + IRtcEngine.GetErrorDescription(errorCode));
           
        }
        Debug.Log("\n\n");
        Debug.Log("---------------OnStreamPublished called----------------");
        Debug.Log("OnStreamPublished url===" + url);
        Debug.Log("OnStreamPublished errorCode===" + errorCode + " = " + IRtcEngine.GetErrorDescription(errorCode));
    }
    const string STREAMKEY_PLACEHOLDER = "h8sf-wfkf-3bcj-zwdq-8bse";
    bool IsCDNAddressReady()
    {
        Debug.LogError("Youtube: " + YTURL + " Key: " + STREAMKEY_PLACEHOLDER);
        if (YTURL.Contains(STREAMKEY_PLACEHOLDER) )
        {
            
            return true;
        }

        return false;
    }
}


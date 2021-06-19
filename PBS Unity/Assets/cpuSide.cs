using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct hairNode{
	public float x;
	public float y;
	public float vx;
	public float vy;
	public int dvx;
	public int dvy;
	public int dummy1;
	public int dummy2;
}

public struct circleCollider{
	public float x;
	public float y;
	public float r;
	public int dvx;
	public int dvy;
	public int dummy1;
	public int dummy2;
	public int dummy3;
}

public class cpuSide : MonoBehaviour {
	static public int kiVelShare;
	static public int kiCalc;
	static public int kiCalcApply;
	static public int kiVisInternodeLines;
	static public int kiPixelsToTexture;
	static public int kiClearPixels;
	static public int kiClearTexture;
	static public int kiOneThreadAction;
	static public int kiInteractionWithColliders;
	static public int nHairs;
	static public float nodeStepSize;
	static public int nodesPerHair;
	static public float gravityForce;
	static public float simulationSpeed;
	static public float strengthOfForces;
	static public RenderTexture renderTexture;
	static public GameObject mainCanvas;
	static public UnityEngine.UI.Image outputImage;
	static public ComputeShader _shader;
	static public hairNode[] hairNodesArray;
	static public ComputeBuffer hairNodesBuffer;
	static public circleCollider[] circleCollidersArray;
	static public ComputeBuffer circleCollidersBuffer;
	static public ComputeBuffer visBuffer;
	static public GameObject[] circleColliderObjects;
	static public float[] debugArray;
	static public ComputeBuffer debugBuffer;
	static public float[] pivotPosition;
	static public float[] pivotActualArray;
	static public ComputeBuffer pivotActualBuffer;
	static public Vector2 oldMouseButton;
	static public int nCircleColliders;
	static public int colliderUnderControlIndex;
	void Start(){
		staticInit();
	}
	static public void staticInit(){
		initTexture();
		initCanvas();
		initData();
		initBuffers();
		initShader();
	}
	static public void initData(){
		int i, hairIndex, nodeIndex;
		//nHairs = 256;
		//nodesPerHair = 128;
		nHairs = 200;
		nodesPerHair = 50;
		nodeStepSize = 5;
		simulationSpeed = 0.0004f;
		strengthOfForces = 10.0f;
		gravityForce = 0.1f;
		hairNodesArray = new hairNode[nHairs * nodesPerHair];
		i = 0;
		while (i < hairNodesArray.Length) {
			hairIndex = i / nodesPerHair;
			nodeIndex = i % nodesPerHair;
			hairNodesArray[i].x = hairIndex - nHairs / 2;
			hairNodesArray[i].y = -nodeStepSize * (nodeIndex - nodesPerHair / 2);
			hairNodesArray[i].vx = 0;
			hairNodesArray[i].vy = 0;
			hairNodesArray[i].dvx = 0;
			hairNodesArray[i].dvy = 0;
			i++;
		}
		circleColliderObjects = GameObject.FindGameObjectsWithTag("circleCollider");
		circleCollidersArray = new circleCollider[circleColliderObjects.Length];
		nCircleColliders = circleColliderObjects.Length;
		i = 0;
		while (i < circleColliderObjects.Length){
			circleCollidersArray[i].x = circleColliderObjects[i].transform.position.x;
			circleCollidersArray[i].y = circleColliderObjects[i].transform.position.y;
			circleCollidersArray[i].r = circleColliderObjects[i].transform.localScale.x * circleColliderObjects[i].GetComponent<CircleCollider2D>().radius;
			i++;
		}
		circleCollidersBuffer = new ComputeBuffer(circleCollidersArray.Length, 4 * 8);

		debugArray = new float[128];
		debugBuffer = new ComputeBuffer(debugArray.Length, 4);

		pivotActualArray = new float[2];
		pivotActualArray[0] = 0;
		pivotActualArray[1] = nodeStepSize * nodesPerHair / 2;
		pivotActualBuffer = new ComputeBuffer(1, 8);
		pivotActualBuffer.SetData(pivotActualArray);
	}
	static void putCollidersDataToArray(){
		int i = 0;
		while (i < circleColliderObjects.Length){
			circleCollidersArray[i].x = circleColliderObjects[i].transform.position.x;
			circleCollidersArray[i].y = circleColliderObjects[i].transform.position.y;
			circleCollidersArray[i].r = circleColliderObjects[i].transform.localScale.x * circleColliderObjects[i].GetComponent<CircleCollider2D>().radius;
			circleCollidersArray[i].dvx = 0;
			circleCollidersArray[i].dvy = 0;
			i++;
		}
	}
	static void initTexture(){
		renderTexture = new RenderTexture(1024, 1024, 32);
		renderTexture.enableRandomWrite = true;
		renderTexture.Create();
	}
	static public void initCanvas(){
		mainCanvas = GameObject.Find("canvas");
		mainCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
		mainCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
		mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
		mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
		mainCanvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 1.0f;
		outputImage = GameObject.Find("canvas/image").GetComponent<UnityEngine.UI.Image>();
		outputImage.color = new Color(1, 1, 1, 1);
		outputImage.material.mainTexture = renderTexture;
		outputImage.type = UnityEngine.UI.Image.Type.Simple;
		//outputImage.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1080);
	}
	static void initBuffers(){
		hairNodesBuffer = new ComputeBuffer(hairNodesArray.Length, 4 * 8);
		hairNodesBuffer.SetData(hairNodesArray);
		visBuffer = new ComputeBuffer(1024 * 1024, 4);
	}
	static void initShader(){
		pivotPosition = new float[2];
		pivotPosition[0] = 0;
		pivotPosition[1] = nodeStepSize * nodesPerHair / 2;
		_shader = Resources.Load<ComputeShader>("shader");

		_shader.SetInt("nNodsPerHair", nodesPerHair);
		_shader.SetInt("nHairs", nHairs);
		_shader.SetInt("nCircleColliders", circleCollidersArray.Length);
		_shader.SetFloat("internodeDistance", nodeStepSize);
		_shader.SetFloats("pivotDestination", pivotPosition);
		_shader.SetFloat("dPosRate", simulationSpeed);
		_shader.SetFloat("dVelRate", strengthOfForces);
		_shader.SetFloat("gravityForce", gravityForce);
		_shader.SetInt("F_TO_I", 2 << 17);
		_shader.SetFloat("I_TO_F", 1f/(2 << 17));
		_shader.SetInt("nCircleColliders", nCircleColliders);

		kiCalc = _shader.FindKernel("calc");
		_shader.SetBuffer(kiCalc, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiCalc, "debugBuffer", debugBuffer);

		kiVelShare = _shader.FindKernel("velShare");
		_shader.SetBuffer(kiVelShare, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiVelShare, "debugBuffer", debugBuffer);

		kiInteractionWithColliders = _shader.FindKernel("interactionWithColliders");
		_shader.SetBuffer(kiInteractionWithColliders, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiInteractionWithColliders, "debugBuffer", debugBuffer);
		_shader.SetBuffer(kiInteractionWithColliders, "circleCollidersBuffer", circleCollidersBuffer);

		kiCalcApply = _shader.FindKernel("calcApply");
		_shader.SetBuffer(kiCalcApply, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiCalcApply, "debugBuffer", debugBuffer);
		_shader.SetBuffer(kiCalcApply, "pivotActual", pivotActualBuffer);

		kiVisInternodeLines = _shader.FindKernel("visInternodeLines");
		_shader.SetBuffer(kiVisInternodeLines, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiVisInternodeLines, "visBuffer", visBuffer);

		kiPixelsToTexture = _shader.FindKernel("pixelsToTexture");
		_shader.SetTexture(kiPixelsToTexture, "renderTexture", renderTexture);
		_shader.SetBuffer(kiPixelsToTexture, "visBuffer", visBuffer);

		kiClearPixels = _shader.FindKernel("clearPixels");
		_shader.SetBuffer(kiClearPixels, "visBuffer", visBuffer);

		kiClearTexture = _shader.FindKernel("clearTexture");
		_shader.SetTexture(kiClearTexture, "renderTexture", renderTexture);

		kiOneThreadAction = _shader.FindKernel("oneThreadAction");
		_shader.SetBuffer(kiOneThreadAction, "debugBuffer", debugBuffer);
		_shader.SetBuffer(kiOneThreadAction, "pivotActual", pivotActualBuffer);
	}
	void Update(){
		doShaderStuff();
		doControls();
	}
	void FixedUpdate(){
		int i;
		Vector2 dv;
		//use colliders data
		i = 0;
		while (i < circleCollidersArray.Length) {
			dv = 0.0000006f * new Vector2(circleCollidersArray[i].dvx, circleCollidersArray[i].dvy);
			circleColliderObjects[i].GetComponent<Rigidbody2D>().AddForce(dv);
			i++;
		}
		putCollidersDataToArray();
	}
	void doShaderStuff(){
		int i, nHairThreadGroups, nNodesThreadGroups;
		nHairThreadGroups = (nHairs - 1) / 16 + 1;
		nNodesThreadGroups = (nodesPerHair - 1) / 8 + 1;
		_shader.SetFloats("pivotDestination", pivotPosition);
		circleCollidersBuffer.SetData(circleCollidersArray);
		i = 0;
		while (i < 40) {
			_shader.Dispatch(kiVelShare, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiCalc, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiInteractionWithColliders, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiCalcApply, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiOneThreadAction, 1, 1, 1);
			i++;
		}
		circleCollidersBuffer.GetData(circleCollidersArray);
		_shader.Dispatch(kiVisInternodeLines, nHairThreadGroups, nNodesThreadGroups, 1);
		_shader.Dispatch(kiClearTexture, 32, 32, 1);
		_shader.Dispatch(kiPixelsToTexture, 32, 32, 1);
		_shader.Dispatch(kiClearPixels, 32, 32, 1);

		//debug
		//debugBuffer.GetData(debugArray);
		//Debug.Log(debugArray[0] + " " + debugArray[1] + "     " + debugArray[2] + " " + debugArray[3] + "     " + debugArray[4] + " " + debugArray[5] + "     " + debugArray[6] + " " + debugArray[7] + "     " + debugArray[8] + " " + debugArray[9] + "     " + debugArray[10] + " " + debugArray[11]);
	}
	void doControls(){
		Vector2 mousePosDelta, rbPos;
		if (Input.GetMouseButtonDown(0))
			oldMouseButton = Camera.main.ScreenToWorldPoint(Input.mousePosition);
		if (Input.GetMouseButtonDown(1)) {
			oldMouseButton = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			colliderUnderControlIndex = 0;
			circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().velocity = Vector2.zero;
			circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
		}
		if (Input.GetMouseButtonUp(1)) {
			circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
		}
		if (Input.GetMouseButton(0)) {
			mousePosDelta = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition) - oldMouseButton;
			oldMouseButton = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			pivotPosition[0] += mousePosDelta.x;
			pivotPosition[1] += mousePosDelta.y;
		}
		if (Input.GetMouseButton(1)) {
			mousePosDelta = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition) - oldMouseButton;
			oldMouseButton = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			rbPos = circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().position;
			rbPos += mousePosDelta;
			circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().position = rbPos;
		}
	}
	void OnDestroy(){					// we need to explicitly release the buffers, otherwise Unity will not be satisfied
		hairNodesBuffer.Release();
		visBuffer.Release();
		circleCollidersBuffer.Release();
		debugBuffer.Release();
		pivotActualBuffer.Release();
	}
}

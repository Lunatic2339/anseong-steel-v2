using UnityEngine;
using UnityEngine.InputSystem;


namespace RobotDelivery {
 public sealed class ShieldController : MonoBehaviour {
  public SkinnedMeshRenderer shield;
  public int blendShapeIndex;
  public float transitionSeconds=1.3333333f;
  public bool inputBlocked;
  public bool enableLocalInput=true;
  [SerializeField] bool deployed;
  [SerializeField,Range(0,1)] float progress;
  InputAction toggleAction,secondaryAction;
  public bool Deployed=>deployed;
  public float Progress=>progress;
  void OnEnable(){InitializeInput();Apply();}
  public void InitializeInput(){
   if(toggleAction!=null)return;
   secondaryAction=new InputAction("ShieldIgnoreFinisherCombo",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
   toggleAction=new InputAction("LocalShieldToggle",InputActionType.Button,"<XRController>{RightHand}/primaryButton");
   toggleAction.AddBinding("<Keyboard>/f8");toggleAction.performed+=OnToggle;
   secondaryAction.Enable();toggleAction.Enable();
  }
  void OnToggle(InputAction.CallbackContext ctx){
   if(inputBlocked||!enableLocalInput)return;
   if(ctx.control.device is not Keyboard && secondaryAction.IsPressed())return;
   Toggle();
  }
  public void Toggle(){deployed=!deployed;}
  public void SetDeployed(bool value){deployed=value;}
  void Update(){Advance(Time.deltaTime);}
  public void Advance(float dt){progress=Mathf.MoveTowards(progress,deployed?1:0,dt/Mathf.Max(.01f,transitionSeconds));Apply();}
  void Apply(){if(shield&&shield.sharedMesh&&blendShapeIndex>=0&&blendShapeIndex<shield.sharedMesh.blendShapeCount)shield.SetBlendShapeWeight(blendShapeIndex,Mathf.SmoothStep(0,1,progress)*100);}
  void OnDisable(){if(toggleAction!=null){toggleAction.performed-=OnToggle;toggleAction.Dispose();secondaryAction.Dispose();toggleAction=null;secondaryAction=null;}}
 }
}

import ctypes, time
from ctypes import wintypes
u = ctypes.WinDLL("user32", use_last_error=True)

# 1) legacy keybd_event
u.keybd_event.argtypes=(wintypes.BYTE,wintypes.BYTE,wintypes.DWORD,ctypes.c_void_p)
ctypes.set_last_error(0)
u.keybd_event(0x41,0,0,None)  # 'A' down
e1=ctypes.get_last_error()
u.keybd_event(0x41,0,2,None)  # up
print("keybd_event lasterr:", e1)

# 2) SendInput unicode
class KI(ctypes.Structure):
    _fields_=[("wVk",wintypes.WORD),("wScan",wintypes.WORD),("dwFlags",wintypes.DWORD),("time",wintypes.DWORD),("dwExtraInfo",ctypes.c_size_t)]
class MI(ctypes.Structure):
    _fields_=[("dx",wintypes.LONG),("dy",wintypes.LONG),("mouseData",wintypes.DWORD),("dwFlags",wintypes.DWORD),("time",wintypes.DWORD),("dwExtraInfo",ctypes.c_size_t)]
class UN(ctypes.Union):
    _fields_=[("mi",MI),("ki",KI)]
class INP(ctypes.Structure):
    _fields_=[("type",wintypes.DWORD),("u",UN)]
u.SendInput.argtypes=(wintypes.UINT,ctypes.POINTER(INP),ctypes.c_int)
inp=INP(type=1,u=UN(ki=KI(wVk=0x42,wScan=0,dwFlags=0,time=0,dwExtraInfo=0)))
ctypes.set_last_error(0)
n=u.SendInput(1,ctypes.byref(inp),ctypes.sizeof(INP))
print("SendInput n=",n,"lasterr=",ctypes.get_last_error(),"sizeof=",ctypes.sizeof(INP))

# 3) BlockInput state probe: try BlockInput(False)
u.BlockInput.argtypes=(wintypes.BOOL,)
ctypes.set_last_error(0)
r=u.BlockInput(False)
print("BlockInput(False) ret=",r,"lasterr=",ctypes.get_last_error())

# 4) GetForegroundWindow + is our session input
print("FG=",u.GetForegroundWindow())
u.GetInputState.restype=wintypes.BOOL
print("GetInputState=",u.GetInputState())

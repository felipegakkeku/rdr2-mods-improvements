import ctypes as C, sys
nv=C.WinDLL("nvapi64.dll"); qi=nv.nvapi_QueryInterface; qi.restype=C.c_void_p; qi.argtypes=[C.c_uint]
def f(i,*a): return C.WINFUNCTYPE(C.c_int,*a)(qi(i))
U=C.c_uint; P=C.c_void_p; S=2048
Init=f(0x0150E828); Create=f(0x0694D52E,C.POINTER(P)); Load=f(0x375DBD6B,P)
FindApp=f(0xEEE566B2,P,C.c_wchar_p,C.POINTER(P),P)
Enum=f(0xAE3039DA,P,P,U,C.POINTER(U),P); Base=f(0xDA8466A0,P,C.POINTER(P))
Name=f(0xD61CBE6E,U,C.c_wchar*S)
class Bin(C.Structure): _fields_=[("len",U),("data",C.c_ubyte*4096)]
class Val(C.Union): _fields_=[("u32",U),("bin",Bin),("wsz",C.c_wchar*S)]
class Set(C.Structure): _fields_=[("ver",U),("name",C.c_wchar*S),("id",U),("type",U),("loc",U),("isCurPre",U),("isPreValid",U),("pre",Val),("cur",Val)]
class App(C.Structure): _fields_=[("ver",U),("isPre",U),("appName",C.c_wchar*S),("friendly",C.c_wchar*S),("launcher",C.c_wchar*S),("fileInFolder",C.c_wchar*S),("bits",U),("cmd",C.c_wchar*S)]
print("init",Init()); s=P(); print("sess",Create(C.byref(s)),"load",Load(s))
def dump(h,label):
    arr=(Set*256)()
    for x in arr: x.ver=C.sizeof(Set)|(1<<16)
    n=U(256); r=Enum(s,h,0,C.byref(n),arr); print("==",label,"enum",r,"n",n.value)
    for x in arr[:n.value]:
        if x.isCurPre: continue
        v = x.cur.u32 if x.type==0 else (x.cur.wsz if x.type==3 else "bin")
        print("  0x%08X %-55s = %s  (loc=%d)"%(x.id,x.name[:55],hex(v) if isinstance(v,int) else v,x.loc))
a=App(); a.ver=C.sizeof(App)|(4<<16); h=P()
for exe in sys.argv[1:]:
    r=FindApp(s,exe,C.byref(h),C.byref(a)); print(exe,"findapp",r,a.appName)
    if r==0: dump(h,"perfil de "+exe)
b=P(); Base(s,C.byref(b)); dump(b,"perfil global (base)")

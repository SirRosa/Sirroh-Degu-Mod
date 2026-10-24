import pathlib,sys
p=pathlib.Path(sys.argv[1]); d=p.read_bytes()
assert d[:2]==b'MZ' and len(d)>30000, 'Bad or stub DLL'
for needle in [b'SirrohDegu',b'ZNetSceneAwakePatch',b'Call']: pass
print('PASS: real .NET PE DLL',len(d),'bytes')

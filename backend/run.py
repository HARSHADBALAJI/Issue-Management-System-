import subprocess, os, sys, time, signal, webbrowser

ROOT_DIR = os.path.dirname(os.path.dirname(__file__))
BACKEND_DIR = os.path.join(os.path.dirname(__file__), 'TicketSystem.Api')
FRONTEND_DIR = os.path.join(ROOT_DIR, 'frontend')
APP_URL = 'http://localhost:5001'

proc = None

def build_frontend():
    print('Building frontend...')
    npm = 'npm.cmd' if sys.platform == 'win32' else 'npm'
    res = subprocess.run([npm, 'run', 'build'], cwd=FRONTEND_DIR, capture_output=True, text=True)
    if res.returncode != 0:
        print('Frontend build failed:')
        print(res.stdout)
        print(res.stderr)
        sys.exit(1)
    print('Frontend built successfully')

def start():
    global proc
    build_frontend()

    print('Starting backend...')
    dotnet = r'C:\Program Files\dotnet\dotnet.exe'
    proc = subprocess.Popen(
        [dotnet, 'run'],
        cwd=BACKEND_DIR,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == 'win32' else 0,
    )

    for _ in range(60):
        time.sleep(1)
        try:
            import urllib.request
            urllib.request.urlopen(APP_URL, timeout=2)
            print(f'Application running at {APP_URL}')
            webbrowser.open(APP_URL)
            return
        except Exception:
            line = proc.stdout.readline().decode(errors='replace').strip()
            if line:
                print(f'  {line}')
    print('Backend failed to start in time')

def stop(signum=None, frame=None):
    if proc:
        print('\nShutting down...')
        proc.terminate()
        proc.wait()
        print('Stopped')
    sys.exit(0)

if __name__ == '__main__':
    signal.signal(signal.SIGINT, stop)
    signal.signal(signal.SIGTERM, stop)
    start()
    print('Press Ctrl+C to stop')
    proc.wait()

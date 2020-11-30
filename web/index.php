<!doctype html>
<html lang="en">

<head>
  <script src="https://ajax.googleapis.com/ajax/libs/jquery/3.2.1/jquery.min.js"></script>
  <link rel="stylesheet" href="index.css">
  <meta content="text/html; charset=UTF-8" http-equiv="content-type">

</head>

<body>

  <div class="div-control">
    <div class="row-div">
      <div class="key k87">
        <p>W</p>
      </div>
    </div>
    <div class="row-div">
      <div class="key k65">
        <p>A</p>
      </div>
      <div class="key k83">
        <p>S</p>
      </div>
      <div class="key k68">
        <p>D</p>
      </div>
    </div>
    <div class="row-div">
      <div>
        <input id="btn1" type="button" value="Подключиться">
      </div>
    </div>
  </div>

  <video id="v" autoplay> </video>
  <form id="somedata" name="somedata" action="#" method="post">
    <div id="log" class="log-field"></div>

  </form>


  <script type="text/javascript">
    $(document).ready(function() {
      var logstrnum = 1;
      var msg;
      var SocketId = 0;
      var message = {
        'w': w,
        'a': a,
        's': s,
        'd': d
      };
      var up = false,
        right = false,
        down = false,
        left = false;
      var w = 0,
        a = 0,
        s = 0,
        d = 0;

      function connectToSock() {
        try {
          var ws = new WebSocket('ws://192.168.2.100:8999/wsDemo/');
        } catch (err) {
          $('#log').append(err + '<br>');
        }

        ws.onopen = function() {
          $('#log').append("Открыто соединение" + '<br>');
        };
        ws.onerror = function(e) {
          $('#log').append("Ошибка соединения" + e.toString() + '<br>');
        };
        ws.onclose = function() {
          $('#log').append("Соединение закрыто" + '<br>');
          ws.close();
          alert("Соединение закрыто");

        };
        ws.onmessage = function(msg) {
          if (event.data instanceof Blob) {
            reader = new FileReader();
            reader.onload = function() {
              $('#log').append(event.data + '<br>');
            };
            reader.readAsText(event.data);
          } else {
            $('#log').append(event.data + '<br>');
          }
        };

        document.addEventListener('keydown', function(e) {
          var key = event.keyCode;
          if (e.keyCode === 38 /* up */ || e.keyCode === 87 /* w */ || e.keyCode === 90 /* z */ ) {
            up = true
          }
          if (e.keyCode === 39 /* right */ || e.keyCode === 68 /* d */ ) {
            right = true
          }
          if (e.keyCode === 40 /* down */ || e.keyCode === 83 /* s */ ) {
            down = true
          }
          if (e.keyCode === 37 /* left */ || e.keyCode === 65 /* a */ || e.keyCode === 81 /* q */ ) {
            left = true
          }
          $('.k' + key).removeClass('active');
          $('.k' + key).addClass('press');

        })

        document.addEventListener('keyup', function(e) {
          var key = event.keyCode;
          if (e.keyCode === 38 /* up */ || e.keyCode === 87 /* w */ || e.keyCode === 90 /* z */ ) {
            up = false
          }
          if (e.keyCode === 39 /* right */ || e.keyCode === 68 /* d */ ) {
            right = false
          }
          if (e.keyCode === 40 /* down */ || e.keyCode === 83 /* s */ ) {
            down = false
          }
          if (e.keyCode === 37 /* left */ || e.keyCode === 65 /* a */ || e.keyCode === 81 /* q */ ) {
            left = false
          }
          $('.k' + key).removeClass('press');
          $('.k' + key).addClass('active');
        })

        function gameLoop() {
          if (up) {
            w = 1;
          } else {
            w = 0;
          }
          if (right) {
            d = 1;
          } else {
            d = 0;
          }
          if (down) {
            s = 1;
          } else {
            s = 0;
          }
          if (left) {
            a = 1;
          } else {
            a = 0;
          }

          if ((w != 0) || (a != 0) || (s != 0) || (d != 0)) {
            console.log('w=' + w, 'a=' + a, 's=' + s, 'd=' + d);
            var val = ('w' + w.toString() + 'a' + a.toString() + 's' + s.toString() + 'd' + d.toString());
            ws.send(val);
            logstrnum = logstrnum + 1;
          }

          setTimeout(gameLoop, 50);
        }



        gameLoop()
      }
      document.getElementById('btn1').onclick = function() {
        connectToSock();
      }
    })


    window.URL = window.URL || window.webkitURL;
    window.MediaSource = window.MediaSource || window.WebKitMediaSource;

    if (!!!window.MediaSource) {
      alert('MediaSource API is not available!');
      return;
    }

    var mediaSource = new MediaSource();
    var video = document.getElementById('v');

    video.src = window.URL.createObjectURL(mediaSource);

    mediaSource.addEventListener('webkitsourceopen', function(e) {
      var sourceBuffer = mediaSource.addSourceBuffer('video/webm; codecs="vorbis,vp8"');
      var socket = io.connect('http://localhost:8080');

      if (socket)
        console.log('Library retrieved!');

      socket.emit('VIDEO_STREAM_REQ', 'REQUEST');

      socket.on('VS', function(data) {
        console.log(data);
        sourceBuffer.append(data);
      });
    });
  </script>
</body>

</html>
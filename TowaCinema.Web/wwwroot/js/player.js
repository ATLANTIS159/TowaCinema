let player;
let container;
let progressbar;
let controls;
let volumeSlider;
let progresslevel;
let time;

let timer;

export function init() {
    player = document.querySelector('.player-screen');
    container = document.querySelector('.player-container');
    progressbar = document.querySelector('.progress-bar');
    controls = document.querySelector('.player-controls');
    volumeSlider = document.querySelector('.player-volume-slider');
    progresslevel = document.querySelector(".progress-level");
    time = document.querySelector(".current-time");

    container.addEventListener("mousemove", () => {
        controls.classList.add("show-controls");
        document.body.style.cursor = "default";
        clearTimeout(timer);
        hideControls();
    });
}

export function initHls(playlist, startTime) {
    if (Hls.isSupported()) {
        let hls;
        if (startTime) {
            const config = {
                startPosition: startTime
            };
            hls = new Hls(config);
        } else {
            hls = new Hls();
        }
        hls.loadSource(playlist);
        hls.attachMedia(player);
    } else if (playlist.canPlayType('application/vnd.apple.mpegurl')) {
        player.src = playlist;
    }
}

export function progressBarMouseMoved(offsetX) {
    let timelineWidth = progressbar.clientWidth;
    let seconds = Math.floor((offsetX / timelineWidth) * player.duration);
    const progressTime = progressbar.querySelector("span");
    offsetX = offsetX < 40 ? 40 : (offsetX > timelineWidth - 40) ? timelineWidth - 40 : offsetX;
    progressTime.style.left = `${offsetX}px`;
    return seconds;
}

export function progressBarClicked(offsetX) {
    let timelineWidth = progressbar.clientWidth;
    player.currentTime = (offsetX / timelineWidth) * player.duration;
}

export function volumeChanged(value) {
    player.volume = value;
    if (player.volume === 0) {
        player.muted = true;
        return 'muted';
    }

    if (player.muted) {
        player.muted = false;
        return "unmuted"
    }

    return "changed";
}

export function setCurrentTime(time) {
    player.currentTime = time;
}

const hideControls = () => {
    if (player.paused) return;
    timer = setTimeout(() => {
        controls.classList.remove("show-controls");
        if (document.fullscreenElement) {
            document.body.style.cursor = "none";
        }
    }, 3000);
}

const formatTime = time => {
    const hours = Math.floor(time / 3600);
    const minutes = Math.floor((time % 3600) / 60);
    const seconds = Math.floor(time % 60);

    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
}

export function dragProgressBar(offsetX) {
    let timelineWidth = progressbar.clientWidth;
    document.querySelector(".progress-level").style.width = `${offsetX}px`;
    player.currentTime = (offsetX / timelineWidth) * player.duration;
    return (parseInt(player.currentTime));
}

export function togglePlayVideo() {
    try {
        if (player.paused) {
            player.play();
            return 'play';
        }
        player.pause();
        return 'pause';
    } catch (e) {
        return 'error';
    }
}

export function toggleVolumeMute() {
    if (player.muted) {
        if (player.volume === 0) {
            volumeSlider.value = 1;
            player.volume = 1;
        }
        player.muted = false;
        return "unmute";
    } else {
        player.muted = true;
        return "mute";
    }
}

export function toggleFullscreen() {
    if (document.fullscreenElement) {
        document.exitFullscreen();
        return 'normal';
    }
    container.requestFullscreen();
    return 'fullscreen';
}

export function togglePip() {
    if (document.pictureInPictureElement) {
        document.exitPictureInPicture();
        return 'normal';
    }
    player.requestPictureInPicture();
    return 'pip';
}

export function skipForward() {
    player.currentTime += 5;
}

export function skipBackward() {
    player.currentTime -= 5;
}

export function updateTime() {
    let currentTime = player.currentTime;
    let duration = player.duration;
    let percent = (currentTime / duration) * 100;
    progresslevel.style.width = `${percent}%`;

    return parseInt(currentTime);
}

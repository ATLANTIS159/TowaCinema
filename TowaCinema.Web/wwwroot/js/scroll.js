export function getScrollPosition(selector) {
    return document.querySelector(selector).scrollTop;
}

export function setScrollPosition(selector, position) {
    document.querySelector(selector).scrollTop = position;
}
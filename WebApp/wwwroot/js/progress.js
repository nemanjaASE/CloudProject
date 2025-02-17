
    document.addEventListener("DOMContentLoaded", function () {
        let scrollContainer = document.querySelector(".progress-vertical-container");
    let scoreLines = document.querySelectorAll(".score-line");

    function updateLineWidth() {
        let scrollWidth = scrollContainer.scrollWidth;
    let visibleWidth = scrollContainer.clientWidth; 
    let scrollLeft = scrollContainer.scrollLeft; 

        let newWidth = visibleWidth + scrollLeft;
        console.log("Nova širina linija:", newWidth);
        scoreLines.forEach(line => {
        line.style.width = newWidth + "px";
        });
    }
    scrollContainer.addEventListener("scroll", updateLineWidth);
    updateLineWidth();
});


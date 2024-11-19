function setResizeHandler(dotNetHelper) {
    function updateWidth() {
        let width = window.innerWidth;

        //const parentDiv = document.getElementById('datasheet-parent');
        //parentDivWidth = parentDiv.clientWidth;

        //console.log(`parentDiv.clientWidth: ${parentDiv.clientWidth}`);
        //console.log(`window.innerWidth: ${window.innerWidth}`);

        const sandwichMenu = document.getElementById('sandwich-menu');
        const isSandwichMenuVisible = sandwichMenu && window.getComputedStyle(sandwichMenu).display !== 'none';


        dotNetHelper.invokeMethodAsync('UpdateMaxWidth', width, isSandwichMenuVisible);
    }

    window.addEventListener('resize', updateWidth);
    updateWidth(); // Set initial width
}
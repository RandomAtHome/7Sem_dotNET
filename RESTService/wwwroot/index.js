window.onload = async function () {
    // Based on https://stackoverflow.com/questions/16215771/how-open-select-file-dialog-via-js/16215950
    var input = document.createElement('input')
    input.type = 'file'

    input.onchange = e => {
        // getting a hold of the file reference
        var file = e.target.files[0]
        var filename = file.name

        // setting up the reader
        var reader = new FileReader()
        reader.readAsDataURL(file) // this is reading as data url

        const HttpPost = new this.XMLHttpRequest()
        HttpPost.open("POST", "./recognize")
        HttpPost.setRequestHeader("Content-Type", "application/json")
        HttpPost.onload = async function () {
            if (this.readyState == 4 && this.status == 200) {
                const json = JSON.parse(HttpPost.response)
                document.getElementById('recRes').innerHTML = "Filepath: " + json.imagePath + "<br>Class: " + json.className + "<br>Probabilty: " + json.certainty 
            }
        }
        // here we tell the reader what to do when it's done reading...
        reader.onload = readerEvent => {
            var content = readerEvent.target.result // this is the content!
            document.getElementById('loadedImage').src = content
            var body = { Name: filename, Content: content.replace(/^data:.+;base64,/, '')}
            HttpPost.send(JSON.stringify(body))
        }
    }

    document.getElementById('loadImageBtn').onclick = _ => {
        input.click()
    }
    document.getElementById('getStatsBtn').onclick = async _ => {
        const response = await fetch('./recognize')
        const json = await response.json()
        let listbox = document.getElementById("stats")
        while (listbox.firstChild) {
            listbox.removeChild(listbox.firstChild);
        }
        for (let i = 0; i < json.length; i++) {
            let e = document.createElement("li")
            e.appendChild(document.createTextNode(json[i]))
            listbox.appendChild(e)
        }
    }
    document.getElementById('purgeStatsBtn').onclick = _ => {
        const HttpDelete = new this.XMLHttpRequest()
        HttpDelete.open("DELETE", "./recognize")
        HttpDelete.send(null)
    }

    document.getElementById('getStatsBtn').click()
}